using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriftLoop.Constants;
using ThriftLoop.Data;
using ThriftLoop.Enums;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;
using ThriftLoop.Services.OrderManagement.Interface;
using ThriftLoop.Services.WalletManagement.Interface;
using ThriftLoop.ViewModels;

namespace ThriftLoop.Controllers;

[Authorize]
public class OrdersController : BaseController
{
    private readonly IItemRepository _itemRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly IWalletService _walletService;
    private readonly IOrderService _orderService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        IItemRepository itemRepository,
        IOrderRepository orderRepository,
        IDeliveryRepository deliveryRepository,
        IWalletService walletService,
        IOrderService orderService,
        ApplicationDbContext context,
        ILogger<OrdersController> logger)
    {
        _itemRepository = itemRepository;
        _orderRepository = orderRepository;
        _deliveryRepository = deliveryRepository;
        _walletService = walletService;
        _orderService = orderService;
        _context = context;
        _logger = logger;
    }

    // ── Helper to check if a user has a complete profile ─────────────────────
    /// <summary>
    /// Returns true only when the user has both a non-empty PhoneNumber and Address.
    /// Required before the user can purchase items.
    /// </summary>
    private async Task<bool> HasCompleteProfileAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        return user is not null
            && !string.IsNullOrWhiteSpace(user.PhoneNumber)
            && !string.IsNullOrWhiteSpace(user.Address);
    }

    /// <summary>
    /// Redirects the user to their profile page with an explanatory message.
    /// </summary>
    private IActionResult RedirectToCompleteProfile(string action)
    {
        TempData["ProfileIncomplete"] =
            $"Please add your phone number and address before you can {action}.";
        return RedirectToAction("Index", "User");
    }

    // ── Helper to initialize chat for non-delivery orders ────────────────────
    /// <summary>
    /// Initializes a chat session between buyer and seller for Halfway or Pickup orders.
    /// This is a placeholder that logs the chat initialization. In production,
    /// this would call a ChatService to create a conversation.
    /// </summary>
    private async Task InitializeChatForOrderAsync(Order order)
    {
        if (order.ChatInitialized)
            return;

        // TODO: Replace with actual ChatService integration
        // var chatSession = await _chatService.CreateConversationAsync(
        //     order.BuyerId, order.SellerId, order.Id, $"Order #{order.Id} - {order.Item?.Title}");

        // For now, generate a placeholder session ID
        order.ChatSessionId = $"chat_order_{order.Id}_{Guid.NewGuid():N}";
        order.ChatInitialized = true;

        _logger.LogInformation(
            "Chat session initialized for Order {OrderId} between Buyer {BuyerId} and Seller {SellerId}. " +
            "Fulfillment method: {FulfillmentMethod}. Session ID: {SessionId}",
            order.Id, order.BuyerId, order.SellerId, order.FulfillmentMethod, order.ChatSessionId);
    }

    // ── P2P CHECKOUT (GET) ─────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Checkout(int itemId)
    {
        var buyerId = ResolveUserId();
        if (buyerId is null) return Unauthorized();

        if (!await HasCompleteProfileAsync(buyerId.Value))
            return RedirectToCompleteProfile("purchase items");

        var item = await _itemRepository.GetByIdWithUserAsync(itemId);
        if (item is null) return NotFound();

        if (item.UserId == buyerId.Value)
        {
            TempData["ErrorMessage"] = "You cannot purchase your own listing.";
            return RedirectToAction("Details", "Items", new { id = itemId });
        }

        var (allowed, reason) = _orderService.CanBuyerCheckout(item, buyerId.Value);
        if (!allowed)
        {
            TempData["ErrorMessage"] = reason;
            return RedirectToAction("Details", "Items", new { id = itemId });
        }

        var existingOrder = await _orderRepository.GetOrderByItemIdAsync(itemId);
        if (existingOrder is not null)
        {
            _logger.LogWarning(
                "User {BuyerId} attempted to re-checkout Item {ItemId} which already has Order {OrderId}.",
                buyerId.Value, itemId, existingOrder.Id);
            TempData["InfoMessage"] = "This item has already been purchased.";
            return RedirectToAction(nameof(MyPurchases));
        }

        var wallet = await _walletService.GetOrCreateWalletAsync(buyerId.Value);
        return View(BuildCheckoutViewModel(item, buyerId.Value, wallet.Balance));
    }

    // ── P2P CONFIRM ORDER (POST) ───────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmOrder(int itemId, PaymentMethod paymentMethod, FulfillmentMethod fulfillmentMethod)
    {
        var buyerId = ResolveUserId();
        if (buyerId is null) return Unauthorized();

        if (!await HasCompleteProfileAsync(buyerId.Value))
            return RedirectToCompleteProfile("purchase items");

        var item = await _itemRepository.GetByIdAsync(itemId);
        if (item is null) return NotFound();

        if (item.UserId == buyerId.Value)
            return Forbid();

        var (allowed, reason) = _orderService.CanBuyerCheckout(item, buyerId.Value);
        if (!allowed)
        {
            TempData["ErrorMessage"] = reason;
            return RedirectToAction("Details", "Items", new { id = itemId });
        }

        var existingOrder = await _orderRepository.GetOrderByItemIdAsync(itemId);
        if (existingOrder is not null)
        {
            TempData["InfoMessage"] = "Your order was already confirmed.";
            return RedirectToAction(nameof(MyPurchases));
        }

        // Validate fulfillment method is allowed by seller
        if (!IsFulfillmentMethodAllowed(item, fulfillmentMethod))
        {
            TempData["ErrorMessage"] = $"The seller does not allow {fulfillmentMethod} for this item.";
            return RedirectToAction(nameof(Checkout), new { itemId });
        }

        decimal itemPrice = item.Price;
        // Delivery fee only applies to Delivery fulfillment method
        decimal deliveryFee = fulfillmentMethod == FulfillmentMethod.Delivery
            ? ItemConstants.DeliveryFee
            : 0m;
        decimal finalPrice = itemPrice + deliveryFee;

        // Determine initial order status based on fulfillment method
        OrderStatus initialStatus = fulfillmentMethod == FulfillmentMethod.Delivery
            ? OrderStatus.Pending
            : OrderStatus.AwaitingMeeting;

        // ── Wallet payment: hold item price in escrow (delivery fee NOT deducted for Delivery method) ──
        if (paymentMethod == PaymentMethod.Wallet)
        {
            // For Delivery method, only hold item price in escrow (buyer pays rider cash for delivery)
            // For Halfway/Pickup, hold full finalPrice (no delivery fee, so just item price)
            decimal escrowAmount = fulfillmentMethod == FulfillmentMethod.Delivery
                ? itemPrice
                : finalPrice;

            var tempOrder = new Order
            {
                ItemId = item.Id,
                BuyerId = buyerId.Value,
                SellerId = item.UserId,
                FinalPrice = finalPrice,
                DeliveryFee = deliveryFee,
                OrderDate = DateTime.UtcNow,
                Status = initialStatus,
                PaymentMethod = PaymentMethod.Wallet,
                FulfillmentMethod = fulfillmentMethod
            };
            await _orderRepository.AddAsync(tempOrder);

            bool held = await _walletService.HoldEscrowAsync(tempOrder.Id, buyerId.Value, escrowAmount);
            if (!held)
            {
                await _orderRepository.DeleteAsync(tempOrder.Id);
                TempData["ErrorMessage"] =
                    $"Insufficient wallet balance. You need ₱{escrowAmount:N2} " +
                    $"but your available balance is too low. Please add funds or choose Cash on Delivery.";
                return RedirectToAction(nameof(Checkout), new { itemId });
            }

            // Only create delivery record for Delivery fulfillment method
            if (fulfillmentMethod == FulfillmentMethod.Delivery)
            {
                await _deliveryRepository.CreateForOrderAsync(tempOrder.Id);
            }
            else
            {
                // Initialize chat for Halfway/Pickup orders
                await InitializeChatForOrderAsync(tempOrder);
                await _orderRepository.UpdateAsync(tempOrder);
            }

            item.Status = ItemStatus.Sold;
            await _itemRepository.UpdateAsync(item);

            _logger.LogInformation(
                "Order {OrderId} created (Wallet) — Item {ItemId}, Buyer {BuyerId}, Seller {SellerId}, " +
                "ItemPrice ₱{ItemPrice}, DeliveryFee ₱{DeliveryFee}, Total ₱{FinalPrice}, " +
                "Fulfillment: {FulfillmentMethod}, Escrow: ₱{EscrowAmount}.",
                tempOrder.Id, item.Id, buyerId.Value, item.UserId, itemPrice, deliveryFee, finalPrice,
                fulfillmentMethod, escrowAmount);

            string successMessage = fulfillmentMethod switch
            {
                FulfillmentMethod.Delivery => $"Order confirmed for '{item.Title}' at ₱{finalPrice:N2}. " +
                    $"₱{itemPrice:N2} is held in escrow and will be released once you confirm delivery. " +
                    $"You will pay the ₱{deliveryFee:N2} delivery fee directly to the rider in cash. " +
                    "A delivery job has been created and is waiting for a rider to accept.",
                FulfillmentMethod.Halfway => $"Order confirmed for '{item.Title}' at ₱{finalPrice:N2}. " +
                    $"₱{itemPrice:N2} is held in escrow. A chat has been opened for you to coordinate a meeting point with the seller. " +
                    "Funds will be released once you confirm receipt.",
                FulfillmentMethod.Pickup => $"Order confirmed for '{item.Title}' at ₱{finalPrice:N2}. " +
                    $"₱{itemPrice:N2} is held in escrow. A chat has been opened for you to arrange pickup with the seller. " +
                    "Funds will be released once you confirm receipt.",
                _ => $"Order confirmed for '{item.Title}' at ₱{finalPrice:N2}. ₱{escrowAmount:N2} is held in escrow."
            };

            TempData["SuccessMessage"] = successMessage;
            return RedirectToAction(nameof(MyPurchases));
        }

        // ── Cash on Delivery ───────────────────────────────────────────────
        var order = new Order
        {
            ItemId = item.Id,
            BuyerId = buyerId.Value,
            SellerId = item.UserId,
            FinalPrice = finalPrice,
            DeliveryFee = deliveryFee,
            OrderDate = DateTime.UtcNow,
            Status = initialStatus,
            PaymentMethod = PaymentMethod.Cash,
            FulfillmentMethod = fulfillmentMethod,
            CashCollectedByRider = false
        };

        await _orderRepository.AddAsync(order);

        // Only create delivery record for Delivery fulfillment method
        if (fulfillmentMethod == FulfillmentMethod.Delivery)
        {
            await _deliveryRepository.CreateForOrderAsync(order.Id);
        }
        else
        {
            // Initialize chat for Halfway/Pickup orders
            await InitializeChatForOrderAsync(order);
            await _orderRepository.UpdateAsync(order);
        }

        item.Status = ItemStatus.Sold;
        await _itemRepository.UpdateAsync(item);

        _logger.LogInformation(
            "Order {OrderId} created (COD) — Item {ItemId}, Buyer {BuyerId}, Seller {SellerId}, " +
            "ItemPrice ₱{ItemPrice}, DeliveryFee ₱{DeliveryFee}, Total ₱{FinalPrice}, Fulfillment: {FulfillmentMethod}.",
            order.Id, item.Id, buyerId.Value, item.UserId, itemPrice, deliveryFee, finalPrice, fulfillmentMethod);

        string codSuccessMessage = fulfillmentMethod switch
        {
            FulfillmentMethod.Delivery => $"Order confirmed for '{item.Title}' at ₱{finalPrice:N2} via Cash on Delivery. " +
                $"You will pay ₱{itemPrice:N2} for the item plus ₱{deliveryFee:N2} delivery fee to the rider. " +
                "A delivery job has been created and is waiting for a rider to accept.",
            FulfillmentMethod.Halfway => $"Order confirmed for '{item.Title}' at ₱{finalPrice:N2} via Cash. " +
                "A chat has been opened for you to coordinate a meeting point with the seller. " +
                "You will pay the seller in cash when you meet.",
            FulfillmentMethod.Pickup => $"Order confirmed for '{item.Title}' at ₱{finalPrice:N2} via Cash. " +
                "A chat has been opened for you to arrange pickup with the seller. " +
                "You will pay the seller in cash when you pick up the item.",
            _ => $"Order confirmed for '{item.Title}' at ₱{finalPrice:N2} via Cash on Delivery."
        };

        TempData["SuccessMessage"] = codSuccessMessage;
        return RedirectToAction(nameof(MyPurchases));
    }

    // ── SHOP CHECKOUT / Option B (GET) ─────────────────────────────────────

    /// <summary>
    /// Renders the checkout page for a shop SKU purchase (Option B).
    /// <paramref name="skuId"/> identifies the exact variant + size the buyer
    /// selected on the Details page. <paramref name="quantity"/> comes from
    /// the quantity picker and is clamped to available stock.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ShopCheckout(int skuId, int quantity = 1)
    {
        var buyerId = ResolveUserId();
        if (buyerId is null) return Unauthorized();

        if (!await HasCompleteProfileAsync(buyerId.Value))
            return RedirectToCompleteProfile("purchase items");

        var sku = await _itemRepository.GetSkuByIdWithItemAsync(skuId);

        if (sku?.Variant?.Item is null) return NotFound();

        var item = sku.Variant.Item;

        if (item.UserId == buyerId.Value)
        {
            TempData["ErrorMessage"] = "You cannot purchase your own listing.";
            return RedirectToAction("Details", "Shop", new { id = item.Id });
        }

        if (sku.Status != SkuStatus.Available || sku.Quantity <= 0)
        {
            TempData["ErrorMessage"] = "Sorry, this variant is out of stock.";
            return RedirectToAction("Details", "Shop", new { id = item.Id });
        }

        // Clamp to available stock — handles stale values from the picker
        quantity = Math.Max(1, Math.Min(quantity, sku.Quantity));

        var wallet = await _walletService.GetOrCreateWalletAsync(buyerId.Value);
        return View("Checkout", BuildShopCheckoutViewModel(item, sku, quantity, wallet.Balance));
    }

    // ── CONFIRM SHOP ORDER / Option B (POST) ──────────────────────────────

    /// <summary>
    /// Confirms a shop order for one SKU at a chosen quantity.
    ///
    /// Stock is decremented inside the same SaveChanges that creates the
    /// order row — first confirm wins. If two buyers race, the one who
    /// arrives second gets an out-of-stock error and is sent back to the
    /// checkout page with the remaining quantity pre-filled.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmShopOrder(int skuId, int quantity, PaymentMethod paymentMethod, FulfillmentMethod fulfillmentMethod)
    {
        var buyerId = ResolveUserId();
        if (buyerId is null) return Unauthorized();

        if (!await HasCompleteProfileAsync(buyerId.Value))
            return RedirectToCompleteProfile("purchase items");

        quantity = Math.Max(1, quantity);

        // Load tracked so we can decrement stock in the same SaveChanges call
        var sku = await _context.ItemVariantSkus
            .Include(s => s.Variant)
                .ThenInclude(v => v.Item)
            .FirstOrDefaultAsync(s => s.Id == skuId);

        if (sku?.Variant?.Item is null) return NotFound();
        var item = sku.Variant.Item;

        if (item.UserId == buyerId.Value) return Forbid();

        // Validate fulfillment method is allowed by seller
        if (!IsFulfillmentMethodAllowed(item, fulfillmentMethod))
        {
            TempData["ErrorMessage"] = $"The seller does not allow {fulfillmentMethod} for this item.";
            return RedirectToAction(nameof(ShopCheckout), new { skuId, quantity });
        }

        // ── First-come-first-served stock check ────────────────────────────
        if (sku.Status != SkuStatus.Available || sku.Quantity < quantity)
        {
            int remaining = Math.Max(0, sku.Quantity);
            TempData["ErrorMessage"] = remaining == 0
                ? "Sorry, this item just sold out while you were checking out."
                : $"Only {remaining} unit(s) left. Please reduce your quantity.";

            return RedirectToAction(nameof(ShopCheckout),
                new { skuId, quantity = Math.Max(1, remaining) });
        }

        decimal itemPrice = sku.Price * quantity;
        // Delivery fee only applies to Delivery fulfillment method
        decimal deliveryFee = fulfillmentMethod == FulfillmentMethod.Delivery
            ? ItemConstants.DeliveryFee
            : 0m;
        decimal finalPrice = itemPrice + deliveryFee;

        // Determine initial order status based on fulfillment method
        OrderStatus initialStatus = fulfillmentMethod == FulfillmentMethod.Delivery
            ? OrderStatus.Pending
            : OrderStatus.AwaitingMeeting;

        // ── Wallet path: create order → hold escrow → decrement stock ──────
        if (paymentMethod == PaymentMethod.Wallet)
        {
            // For Delivery method, only hold item price in escrow
            decimal escrowAmount = fulfillmentMethod == FulfillmentMethod.Delivery
                ? itemPrice
                : finalPrice;

            var tempOrder = new Order
            {
                ItemId = item.Id,
                ItemVariantSkuId = sku.Id,
                BuyerId = buyerId.Value,
                SellerId = item.UserId,
                FinalPrice = finalPrice,
                DeliveryFee = deliveryFee,
                Quantity = quantity,
                OrderDate = DateTime.UtcNow,
                Status = initialStatus,
                PaymentMethod = PaymentMethod.Wallet,
                FulfillmentMethod = fulfillmentMethod
            };
            await _orderRepository.AddAsync(tempOrder);

            bool held = await _walletService.HoldEscrowAsync(tempOrder.Id, buyerId.Value, escrowAmount);
            if (!held)
            {
                await _orderRepository.DeleteAsync(tempOrder.Id);
                TempData["ErrorMessage"] =
                    $"Insufficient wallet balance. You need ₱{escrowAmount:N2} " +
                    $"but your available balance is too low. Please add funds or choose Cash on Delivery.";
                return RedirectToAction(nameof(ShopCheckout), new { skuId, quantity });
            }

            // Only create delivery record for Delivery fulfillment method
            if (fulfillmentMethod == FulfillmentMethod.Delivery)
            {
                await _deliveryRepository.CreateForOrderAsync(tempOrder.Id);
            }
            else
            {
                // Initialize chat for Halfway/Pickup orders
                await InitializeChatForOrderAsync(tempOrder);
                await _orderRepository.UpdateAsync(tempOrder);
            }

            // Decrement stock — if it hits zero, mark the SKU sold
            sku.Quantity -= quantity;
            if (sku.Quantity == 0) sku.Status = SkuStatus.Sold;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Shop Order {OrderId} (Wallet) — SKU {SkuId} ×{Qty}, Buyer {BuyerId}, " +
                "Seller {SellerId}, ItemPrice ₱{ItemPrice}, DeliveryFee ₱{DeliveryFee}, Total ₱{FinalPrice}, " +
                "Fulfillment: {FulfillmentMethod}, Escrow: ₱{EscrowAmount}.",
                tempOrder.Id, sku.Id, quantity, buyerId.Value, item.UserId, itemPrice, deliveryFee, finalPrice,
                fulfillmentMethod, escrowAmount);

            string successMessage = fulfillmentMethod switch
            {
                FulfillmentMethod.Delivery => $"Order confirmed for '{item.Title}' ×{quantity} at ₱{finalPrice:N2}. " +
                    $"₱{itemPrice:N2} is held in escrow. You will pay the ₱{deliveryFee:N2} delivery fee to the rider in cash. " +
                    "A delivery job has been created and is waiting for a rider to accept.",
                FulfillmentMethod.Halfway => $"Order confirmed for '{item.Title}' ×{quantity} at ₱{finalPrice:N2}. " +
                    $"₱{itemPrice:N2} is held in escrow. A chat has been opened to coordinate a meeting point.",
                FulfillmentMethod.Pickup => $"Order confirmed for '{item.Title}' ×{quantity} at ₱{finalPrice:N2}. " +
                    $"₱{itemPrice:N2} is held in escrow. A chat has been opened to arrange pickup.",
                _ => $"Order confirmed for '{item.Title}' ×{quantity} at ₱{finalPrice:N2}."
            };

            TempData["SuccessMessage"] = successMessage;
            return RedirectToAction(nameof(MyPurchases));
        }

        // ── Cash on Delivery path ──────────────────────────────────────────
        var order = new Order
        {
            ItemId = item.Id,
            ItemVariantSkuId = sku.Id,
            BuyerId = buyerId.Value,
            SellerId = item.UserId,
            FinalPrice = finalPrice,
            DeliveryFee = deliveryFee,
            Quantity = quantity,
            OrderDate = DateTime.UtcNow,
            Status = initialStatus,
            PaymentMethod = PaymentMethod.Cash,
            FulfillmentMethod = fulfillmentMethod,
            CashCollectedByRider = false
        };
        await _orderRepository.AddAsync(order);

        // Only create delivery record for Delivery fulfillment method
        if (fulfillmentMethod == FulfillmentMethod.Delivery)
        {
            await _deliveryRepository.CreateForOrderAsync(order.Id);
        }
        else
        {
            // Initialize chat for Halfway/Pickup orders
            await InitializeChatForOrderAsync(order);
            await _orderRepository.UpdateAsync(order);
        }

        sku.Quantity -= quantity;
        if (sku.Quantity == 0) sku.Status = SkuStatus.Sold;
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Shop Order {OrderId} (COD) — SKU {SkuId} ×{Qty}, Buyer {BuyerId}, " +
            "Seller {SellerId}, ItemPrice ₱{ItemPrice}, DeliveryFee ₱{DeliveryFee}, Total ₱{FinalPrice}, " +
            "Fulfillment: {FulfillmentMethod}.",
            order.Id, sku.Id, quantity, buyerId.Value, item.UserId, itemPrice, deliveryFee, finalPrice, fulfillmentMethod);

        string codSuccessMessage = fulfillmentMethod switch
        {
            FulfillmentMethod.Delivery => $"Order confirmed for '{item.Title}' ×{quantity} at ₱{finalPrice:N2} via Cash on Delivery. " +
                "A delivery job has been created and is waiting for a rider to accept.",
            FulfillmentMethod.Halfway => $"Order confirmed for '{item.Title}' ×{quantity} at ₱{finalPrice:N2} via Cash. " +
                "A chat has been opened to coordinate a meeting point. You will pay the seller in cash when you meet.",
            FulfillmentMethod.Pickup => $"Order confirmed for '{item.Title}' ×{quantity} at ₱{finalPrice:N2} via Cash. " +
                "A chat has been opened to arrange pickup. You will pay the seller in cash when you pick up the item.",
            _ => $"Order confirmed for '{item.Title}' ×{quantity} at ₱{finalPrice:N2} via Cash on Delivery."
        };

        TempData["SuccessMessage"] = codSuccessMessage;
        return RedirectToAction(nameof(MyPurchases));
    }

    // ── CART CHECKOUT (GET) ──────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> CartCheckout([FromQuery] int[] cartItemIds)
    {
        var buyerId = ResolveUserId();
        if (buyerId is null) return Unauthorized();

        if (!await HasCompleteProfileAsync(buyerId.Value))
            return RedirectToCompleteProfile("checkout items from your cart");

        if (cartItemIds == null || cartItemIds.Length == 0)
        {
            TempData["ErrorMessage"] = "Please select at least one item to checkout.";
            return RedirectToAction("Index", "Cart");
        }

        var cartItems = await _context.CartItems
            .Include(ci => ci.Item).ThenInclude(i => i!.Shop)
            .Include(ci => ci.ItemVariantSku).ThenInclude(s => s!.Variant)
            .Where(ci => cartItemIds.Contains(ci.Id) && ci.UserId == buyerId.Value)
            .ToListAsync();

        if (cartItems.Count == 0)
        {
            TempData["ErrorMessage"] = "Selected items not found in your cart.";
            return RedirectToAction("Index", "Cart");
        }

        var outOfStock = cartItems.Where(ci => ci.ItemVariantSku!.Quantity < ci.Quantity).ToList();
        if (outOfStock.Count > 0)
        {
            TempData["ErrorMessage"] = $"Some items are out of stock.";
            return RedirectToAction("Index", "Cart");
        }

        var wallet = await _walletService.GetOrCreateWalletAsync(buyerId.Value);
        var viewModel = BuildCartCheckoutViewModel(cartItems, buyerId.Value, wallet.Balance);

        return View("CartCheckout", viewModel);
    }

    // ── CART CHECKOUT (POST) ────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CartCheckoutConfirm([FromForm] int[] cartItemIds, [FromForm] string paymentMethod)
    {
        var buyerId = ResolveUserId();
        if (buyerId is null) return Unauthorized();

        if (!await HasCompleteProfileAsync(buyerId.Value))
            return RedirectToCompleteProfile("checkout items from your cart");

        if (cartItemIds == null || cartItemIds.Length == 0)
        {
            TempData["ErrorMessage"] = "Please select at least one item to checkout.";
            return RedirectToAction("Index", "Cart");
        }

        var cartItems = await _context.CartItems
            .Include(ci => ci.Item).ThenInclude(i => i!.Shop)
            .Include(ci => ci.ItemVariantSku).ThenInclude(s => s!.Variant)
            .Where(ci => cartItemIds.Contains(ci.Id) && ci.UserId == buyerId.Value)
            .ToListAsync();

        if (cartItems.Count == 0)
        {
            TempData["ErrorMessage"] = "Selected items not found in your cart.";
            return RedirectToAction("Index", "Cart");
        }

        var isWalletPayment = paymentMethod == "wallet";
        var wallet = await _walletService.GetOrCreateWalletAsync(buyerId.Value);
        var createdOrderIds = new List<int>();

        // Group items by seller (each seller gets one order with all their items)
        var groupedBySeller = cartItems.GroupBy(ci => ci.Item!.UserId).ToList();

        // Calculate total cost - delivery fee only for sellers that allow delivery (simplified: assume delivery)
        decimal subtotal = cartItems.Sum(ci => ci.ItemVariantSku!.Price * ci.Quantity);
        decimal totalDeliveryFee = groupedBySeller.Count * ItemConstants.DeliveryFee;
        decimal grandTotal = subtotal + totalDeliveryFee;

        if (isWalletPayment && wallet.Balance < grandTotal)
        {
            TempData["ErrorMessage"] =
                $"Insufficient wallet balance. You need ₱{grandTotal:N2} but your balance is ₱{wallet.Balance:N2}.";
            return RedirectToAction(nameof(CartCheckout), new { cartItemIds });
        }

        // Use the execution strategy for retry support
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Create one order per seller
                foreach (var sellerGroup in groupedBySeller)
                {
                    var sellerId = sellerGroup.Key;
                    var sellerItems = sellerGroup.ToList();

                    // Calculate totals for this seller's order
                    decimal sellerSubtotal = sellerItems.Sum(ci => ci.ItemVariantSku!.Price * ci.Quantity);
                    decimal sellerFinalPrice = sellerSubtotal + ItemConstants.DeliveryFee;

                    // Create the order for this seller
                    var order = new Order
                    {
                        BuyerId = buyerId.Value,
                        SellerId = sellerId,
                        FinalPrice = sellerFinalPrice,
                        DeliveryFee = ItemConstants.DeliveryFee,
                        Quantity = sellerItems.Sum(ci => ci.Quantity),
                        OrderDate = DateTime.UtcNow,
                        Status = OrderStatus.Pending,
                        PaymentMethod = isWalletPayment ? PaymentMethod.Wallet : PaymentMethod.Cash,
                        FulfillmentMethod = FulfillmentMethod.Delivery, // Cart checkout always uses Delivery for now
                        CashCollectedByRider = false
                    };

                    // Set ItemId to the first item's ID (for backward compatibility)
                    order.ItemId = sellerItems.First().ItemId;

                    await _orderRepository.AddAsync(order);
                    await _context.SaveChangesAsync();

                    // Create OrderItem records for each cart item in this order
                    foreach (var ci in sellerItems)
                    {
                        var sku = ci.ItemVariantSku!;
                        var item = ci.Item!;
                        var qty = ci.Quantity;

                        // Validate stock
                        if (sku.Quantity < qty)
                            throw new InvalidOperationException($"Item '{item.Title}' is now out of stock.");

                        // Create OrderItem linking the SKU to the order
                        var orderItem = new OrderItem
                        {
                            OrderId = order.Id,
                            ItemVariantSkuId = sku.Id,
                            Quantity = qty,
                            UnitPrice = sku.Price
                        };
                        _context.OrderItems.Add(orderItem);

                        // Decrement stock
                        sku.Quantity -= qty;
                        if (sku.Quantity == 0) sku.Status = SkuStatus.Sold;

                        // Remove from cart
                        _context.CartItems.Remove(ci);
                    }

                    await _context.SaveChangesAsync();

                    // Handle wallet payment escrow
                    if (isWalletPayment)
                    {
                        // For cart checkout, hold full amount in escrow
                        bool held = await _walletService.HoldEscrowAsync(order.Id, buyerId.Value, sellerFinalPrice);
                        if (!held)
                        {
                            throw new InvalidOperationException($"Wallet deduction failed for order from seller {sellerId}.");
                        }
                    }

                    // Create delivery record for this order
                    await _deliveryRepository.CreateForOrderAsync(order.Id);

                    createdOrderIds.Add(order.Id);
                }

                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Cart Checkout — Buyer {BuyerId} created {OrderCount} orders from {ItemCount} items across {SellerCount} sellers. Total: ₱{Total}",
                    buyerId.Value, createdOrderIds.Count, cartItems.Count, groupedBySeller.Count, grandTotal);

                TempData["SuccessMessage"] =
                    $"Order{(createdOrderIds.Count > 1 ? "s" : "")} confirmed for {cartItems.Count} item{(cartItems.Count > 1 ? "s" : "")} " +
                    $"from {groupedBySeller.Count} seller{(groupedBySeller.Count > 1 ? "s" : "")}. Total: ₱{grandTotal:N2}. " +
                    "A delivery job has been created for each order.";

                return RedirectToAction(nameof(MyPurchases));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Cart checkout failed for Buyer {BuyerId}", buyerId.Value);
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Index", "Cart");
            }
        });
    }

    // ── MARK DELIVERED (Buyer confirms receipt) ────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkDelivered(int orderId)
    {
        var buyerId = ResolveUserId();
        if (buyerId is null) return Unauthorized();

        var order = await _orderRepository.GetOrderByIdAsync(orderId);
        if (order is null) return NotFound();

        if (order.BuyerId != buyerId.Value)
            return Forbid();

        if (order.Status == OrderStatus.Completed)
        {
            TempData["InfoMessage"] = "This order is already marked as delivered.";
            return RedirectToAction(nameof(MyPurchases));
        }

        if (order.Status == OrderStatus.Cancelled)
        {
            TempData["ErrorMessage"] = "Cannot mark a cancelled order as delivered.";
            return RedirectToAction(nameof(MyPurchases));
        }

        // Handle non-delivery orders (Halfway/Pickup) differently
        if (order.FulfillmentMethod != FulfillmentMethod.Delivery)
        {
            return await MarkNonDeliveryOrderCompleted(order, buyerId.Value);
        }

        // Confirm delivery through the delivery system.
        var delivery = await _deliveryRepository.GetByOrderIdAsync(orderId);
        if (delivery == null)
        {
            TempData["ErrorMessage"] = "Delivery record not found.";
            return RedirectToAction(nameof(MyPurchases));
        }

        if (delivery.Status != DeliveryStatus.Delivered)
        {
            TempData["ErrorMessage"] = "You can only confirm delivery after the rider has marked it as delivered.";
            return RedirectToAction(nameof(MyPurchases));
        }

        int? riderUserId = delivery.RiderId;
        if (riderUserId is null)
        {
            TempData["ErrorMessage"] = "Cannot process payment: no rider has been assigned to this delivery.";
            return RedirectToAction(nameof(MyPurchases));
        }

        // Amounts for the two payment recipients.
        decimal itemPrice = order.FinalPrice - order.DeliveryFee;   // goes to seller
        decimal deliveryFee = order.DeliveryFee;                    // goes to rider

        // ── Process payment based on method ───────────────────────────────
        if (order.PaymentMethod == PaymentMethod.Wallet)
        {
            // Release item price from escrow → seller.
            await _walletService.ReleaseEscrowAsync(
                order.Id, order.BuyerId, order.SellerId, itemPrice);

            // Release delivery-fee slice from escrow → rider.
            await _walletService.PayRiderAsync(
                order.Id, order.BuyerId, riderUserId.Value, deliveryFee, fromEscrow: true);
        }
        else if (order.PaymentMethod == PaymentMethod.Cash && !order.CashCollectedByRider)
        {
            // Credit the seller their item price (cash was collected physically).
            await _walletService.RecordCashCollectionAsync(
                order.Id, order.BuyerId, order.SellerId, itemPrice);

            // Credit the rider their delivery fee (buyer paid them in cash).
            await _walletService.PayRiderAsync(
                order.Id, order.BuyerId, riderUserId.Value, deliveryFee, fromEscrow: false);
        }

        // Confirm delivery completion
        var confirmed = await _deliveryRepository.ConfirmByBuyerAsync(delivery.Id, buyerId.Value);

        if (!confirmed)
        {
            TempData["ErrorMessage"] = "Failed to confirm delivery. Please try again.";
            return RedirectToAction(nameof(MyPurchases));
        }

        _logger.LogInformation(
            "Order {OrderId} marked Completed by Buyer {BuyerId}. " +
            "Seller received ₱{ItemPrice}, Rider received ₱{DeliveryFee}.",
            orderId, buyerId.Value, itemPrice, deliveryFee);

        TempData["SuccessMessage"] =
            $"Delivery confirmed! The seller has been paid ₱{itemPrice:N2} and the rider received their ₱{deliveryFee:N2} delivery fee. Thank you!";

        return RedirectToAction(nameof(MyPurchases));
    }

    // ── MARK NON-DELIVERY ORDER COMPLETED ───────────────────────────────────

    /// <summary>
    /// Marks a Halfway or Pickup order as completed.
    /// Releases escrow or processes cash payment without rider involvement.
    /// </summary>
    private async Task<IActionResult> MarkNonDeliveryOrderCompleted(Order order, int buyerId)
    {
        decimal itemPrice = order.FinalPrice - order.DeliveryFee; // DeliveryFee is 0 for non-delivery

        if (order.PaymentMethod == PaymentMethod.Wallet)
        {
            // Release item price from escrow → seller
            await _walletService.ReleaseEscrowAsync(
                order.Id, order.BuyerId, order.SellerId, itemPrice);
        }
        else if (order.PaymentMethod == PaymentMethod.Cash && !order.CashCollectedByRider)
        {
            // Credit the seller their item price (buyer paid cash)
            await _walletService.RecordCashCollectionAsync(
                order.Id, order.BuyerId, order.SellerId, itemPrice);
            order.CashCollectedByRider = true;
        }

        order.Status = OrderStatus.Completed;
        await _orderRepository.UpdateAsync(order);

        _logger.LogInformation(
            "Non-delivery Order {OrderId} marked Completed by Buyer {BuyerId}. " +
            "Fulfillment: {FulfillmentMethod}. Seller received ₱{ItemPrice}.",
            order.Id, buyerId, order.FulfillmentMethod, itemPrice);

        TempData["SuccessMessage"] =
            $"Order confirmed! The seller has been paid ₱{itemPrice:N2}. Thank you for using ThriftLoop!";

        return RedirectToAction(nameof(MyPurchases));
    }

    // ── MY PURCHASES ───────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> MyPurchases()
    {
        var buyerId = ResolveUserId();
        if (buyerId is null) return Unauthorized();

        var orders = await _orderRepository.GetOrdersByBuyerIdAsync(buyerId.Value);

        _logger.LogInformation(
            "User {BuyerId} viewed My Purchases ({Count} orders).", buyerId.Value, orders.Count);

        return View(orders);
    }

    // ── Private helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Checks if the selected fulfillment method is allowed by the seller for this item.
    /// </summary>
    private static bool IsFulfillmentMethodAllowed(Item item, FulfillmentMethod method)
    {
        return method switch
        {
            FulfillmentMethod.Delivery => item.AllowDelivery,
            FulfillmentMethod.Halfway => item.AllowHalfway,
            FulfillmentMethod.Pickup => item.AllowPickup,
            _ => false
        };
    }

    /// <summary>Builds the CheckoutViewModel for a P2P / Stealable item.</summary>
    private static CheckoutViewModel BuildCheckoutViewModel(Item item, int buyerId, decimal buyerBalance)
    {
        bool wasStolen = item.ListingType == ListingType.Stealable
                      && item.Status == ItemStatus.StolenPendingCheckout
                      && item.CurrentWinnerId == buyerId;

        decimal basePrice = wasStolen
            ? item.Price - ItemConstants.StealPremium
            : item.Price;

        string sellerEmail = item.User?.Email ?? string.Empty;
        string sellerDisplay = sellerEmail.Contains('@')
            ? sellerEmail.Split('@')[0]
            : sellerEmail;

        return new CheckoutViewModel
        {
            ItemId = item.Id,
            ItemTitle = item.Title,
            ItemImageUrl = item.ImageUrl,
            ItemCategory = item.Category,
            ItemCondition = item.Condition,
            ItemSize = item.Size,
            SellerName = sellerDisplay,
            SellerEmail = sellerEmail,
            BasePrice = basePrice,
            WasStolen = wasStolen,
            IsStealable = item.ListingType == ListingType.Stealable,
            BuyerBalance = buyerBalance,
            AllowDelivery = item.AllowDelivery,
            AllowHalfway = item.AllowHalfway,
            AllowPickup = item.AllowPickup
        };
    }

    /// <summary>Builds the CheckoutViewModel for a shop SKU purchase (Option B).</summary>
    private static CheckoutViewModel BuildShopCheckoutViewModel(
        Item item, ItemVariantSku sku, int quantity, decimal buyerBalance)
    {
        string sellerEmail = item.User?.Email ?? string.Empty;
        string sellerDisplay = sellerEmail.Contains('@')
            ? sellerEmail.Split('@')[0]
            : sellerEmail;

        return new CheckoutViewModel
        {
            ItemId = item.Id,
            ItemTitle = item.Title,
            ItemImageUrl = item.ImageUrl,
            ItemCategory = item.Category,
            ItemCondition = item.Condition,
            SellerName = sellerDisplay,
            SellerEmail = sellerEmail,
            BasePrice = sku.Price,
            IsShopOrder = true,
            SkuId = sku.Id,
            Quantity = quantity,
            MaxQuantity = sku.Quantity,
            SelectedVariantName = sku.Variant?.Name ?? string.Empty,
            SelectedSize = sku.Size,
            BuyerBalance = buyerBalance,
            WasStolen = false,
            IsStealable = false,
            AllowDelivery = item.AllowDelivery,
            AllowHalfway = item.AllowHalfway,
            AllowPickup = item.AllowPickup
        };
    }

    /// <summary>Builds a multi-item CheckoutViewModel for cart checkout.</summary>
    private static CartCheckoutViewModel BuildCartCheckoutViewModel(
        IReadOnlyList<CartItem> cartItems, int buyerId, decimal buyerBalance)
    {
        var checkoutItems = cartItems.Select(ci => new CartCheckoutItemViewModel
        {
            CartItemId = ci.Id,
            ItemId = ci.ItemId,
            ItemTitle = ci.Item?.Title ?? "Unknown",
            ItemImageUrl = ci.Item?.ImageUrl,
            ShopId = ci.Item?.ShopId,
            ShopName = ci.Item?.Shop?.ShopName,
            VariantName = ci.ItemVariantSku?.Variant?.Name ?? "",
            Size = ci.ItemVariantSku?.Size,
            Price = ci.ItemVariantSku?.Price ?? 0,
            Quantity = ci.Quantity,
            LineTotal = (ci.ItemVariantSku?.Price ?? 0) * ci.Quantity,
            AvailableStock = ci.ItemVariantSku?.Quantity ?? 0
        }).ToList();

        var groupedByShop = checkoutItems.GroupBy(i => i.ShopId).ToList();
        var subtotal = checkoutItems.Sum(i => i.LineTotal);
        var totalDeliveryFee = groupedByShop.Count * ItemConstants.DeliveryFee;

        return new CartCheckoutViewModel
        {
            Items = checkoutItems,
            BuyerBalance = buyerBalance,
            Subtotal = subtotal,
            DeliveryFeePerShop = ItemConstants.DeliveryFee,
            ShopCount = groupedByShop.Count,
            TotalDeliveryFee = totalDeliveryFee,
            GrandTotal = subtotal + totalDeliveryFee
        };
    }
}