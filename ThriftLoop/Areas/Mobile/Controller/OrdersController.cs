// Areas/Mobile/Controllers/OrdersController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriftLoop.Constants;
using ThriftLoop.Controllers;
using ThriftLoop.Data;
using ThriftLoop.Enums;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;
using ThriftLoop.Services.OrderManagement.Interface;
using ThriftLoop.Services.WalletManagement.Interface;
using ThriftLoop.Services.Interface;
using ThriftLoop.ViewModels;

namespace ThriftLoop.Areas.Mobile.Controllers;

[Area("Mobile")]
[Route("mobile/[controller]/[action]")]
[Authorize]
public class OrdersController : BaseController
{
    private readonly IItemRepository _itemRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly IWalletService _walletService;
    private readonly IOrderService _orderService;
    private readonly IChatService _chatService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        IItemRepository itemRepository,
        IOrderRepository orderRepository,
        IDeliveryRepository deliveryRepository,
        IWalletService walletService,
        IOrderService orderService,
        IChatService chatService,
        ApplicationDbContext context,
        ILogger<OrdersController> logger)
    {
        _itemRepository = itemRepository;
        _orderRepository = orderRepository;
        _deliveryRepository = deliveryRepository;
        _walletService = walletService;
        _orderService = orderService;
        _chatService = chatService;
        _context = context;
        _logger = logger;
    }

    // ── Helper to check if a user has a complete profile ─────────────────────
    private async Task<bool> HasCompleteProfileAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        return user is not null
            && !string.IsNullOrWhiteSpace(user.PhoneNumber)
            && !string.IsNullOrWhiteSpace(user.Address);
    }

    private IActionResult RedirectToCompleteProfile(string action)
    {
        TempData["ProfileIncomplete"] =
            $"Please add your phone number and address before you can {action}.";
        return RedirectToAction("Index", "User", new { area = "Mobile" });
    }

    // ── Helper to initialize chat for non-delivery orders ────────────────────
    private async Task InitializeChatForOrderAsync(Order order)
    {
        if (order.ChatInitialized)
            return;

        try
        {
            var conversationId = await _chatService.InitializeOrderChatAsync(order.Id);

            if (conversationId > 0)
            {
                order.ChatConversationId = conversationId;
                order.ChatInitialized = true;

                _logger.LogInformation(
                    "Mobile: Chat initialized for Order {OrderId}. Fulfillment: {FulfillmentMethod}. Conversation: {ConversationId}",
                    order.Id, order.FulfillmentMethod, conversationId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Mobile: Failed to initialize chat for Order {OrderId}.", order.Id);

            order.ChatSessionId = $"chat_order_{order.Id}_{Guid.NewGuid():N}";
            order.ChatInitialized = true;
        }
    }

    // ── P2P CHECKOUT (GET) ─────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Checkout(int itemId, FulfillmentMethod? fulfillmentMethod = null)
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
            return RedirectToAction("Details", "Items", new { area = "Mobile", id = itemId });
        }

        var (allowed, reason) = _orderService.CanBuyerCheckout(item, buyerId.Value);
        if (!allowed)
        {
            TempData["ErrorMessage"] = reason;
            return RedirectToAction("Details", "Items", new { area = "Mobile", id = itemId });
        }

        var existingOrder = await _orderRepository.GetOrderByItemIdAsync(itemId);
        if (existingOrder is not null)
        {
            _logger.LogWarning(
                "Mobile: User {BuyerId} attempted to re-checkout Item {ItemId} which already has Order {OrderId}.",
                buyerId.Value, itemId, existingOrder.Id);
            TempData["InfoMessage"] = "This item has already been purchased.";
            return RedirectToAction(nameof(MyPurchases));
        }

        var wallet = await _walletService.GetOrCreateWalletAsync(buyerId.Value);
        var viewModel = BuildCheckoutViewModel(item, buyerId.Value, wallet.Balance);

        if (fulfillmentMethod.HasValue)
        {
            viewModel.SelectedFulfillmentMethod = fulfillmentMethod.Value.ToString();
        }

        return View(viewModel);
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
            return RedirectToAction("Details", "Items", new { area = "Mobile", id = itemId });
        }

        var existingOrder = await _orderRepository.GetOrderByItemIdAsync(itemId);
        if (existingOrder is not null)
        {
            TempData["InfoMessage"] = "Your order was already confirmed.";
            return RedirectToAction(nameof(MyPurchases));
        }

        if (!IsFulfillmentMethodAllowed(item, fulfillmentMethod))
        {
            TempData["ErrorMessage"] = $"The seller does not allow {fulfillmentMethod} for this item.";
            return RedirectToAction(nameof(Checkout), new { itemId });
        }

        decimal itemPrice = item.Price;
        decimal deliveryFee = fulfillmentMethod == FulfillmentMethod.Delivery
            ? ItemConstants.DeliveryFee
            : 0m;
        decimal finalPrice = itemPrice + deliveryFee;

        OrderStatus initialStatus = fulfillmentMethod == FulfillmentMethod.Delivery
            ? OrderStatus.Pending
            : OrderStatus.AwaitingMeeting;

        if (paymentMethod == PaymentMethod.Wallet)
        {
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
                    $"Insufficient wallet balance. You need ₱{escrowAmount:N2}. Please add funds or choose Cash on Delivery.";
                return RedirectToAction(nameof(Checkout), new { itemId });
            }

            if (fulfillmentMethod == FulfillmentMethod.Delivery)
            {
                await _deliveryRepository.CreateForOrderAsync(tempOrder.Id);
            }
            else
            {
                await InitializeChatForOrderAsync(tempOrder);
                await _orderRepository.UpdateAsync(tempOrder);
            }

            item.Status = ItemStatus.Sold;
            await _itemRepository.UpdateAsync(item);

            _logger.LogInformation(
                "Mobile: Order {OrderId} created (Wallet) — Item {ItemId}, ₱{FinalPrice}, Fulfillment: {FulfillmentMethod}.",
                tempOrder.Id, item.Id, finalPrice, fulfillmentMethod);

            TempData["SuccessMessage"] = fulfillmentMethod switch
            {
                FulfillmentMethod.Delivery => $"Order confirmed! ₱{itemPrice:N2} held in escrow. Pay ₱{deliveryFee:N2} delivery fee to the rider in cash.",
                FulfillmentMethod.Halfway => $"Order confirmed! ₱{itemPrice:N2} held in escrow. A chat has been opened to coordinate a meeting point.",
                FulfillmentMethod.Pickup => $"Order confirmed! ₱{itemPrice:N2} held in escrow. A chat has been opened to arrange pickup.",
                _ => $"Order confirmed for ₱{finalPrice:N2}."
            };

            if (fulfillmentMethod == FulfillmentMethod.Halfway || fulfillmentMethod == FulfillmentMethod.Pickup)
            {
                if (tempOrder.ChatConversationId.HasValue)
                {
                    return RedirectToAction("Conversation", "Chat", new { area = "Mobile", id = tempOrder.ChatConversationId.Value });
                }
            }

            return RedirectToAction(nameof(MyPurchases));
        }

        // Cash on Delivery
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

        if (fulfillmentMethod == FulfillmentMethod.Delivery)
        {
            await _deliveryRepository.CreateForOrderAsync(order.Id);
        }
        else
        {
            await InitializeChatForOrderAsync(order);
            await _orderRepository.UpdateAsync(order);
        }

        item.Status = ItemStatus.Sold;
        await _itemRepository.UpdateAsync(item);

        _logger.LogInformation(
            "Mobile: Order {OrderId} created (COD) — Item {ItemId}, ₱{FinalPrice}, Fulfillment: {FulfillmentMethod}.",
            order.Id, item.Id, finalPrice, fulfillmentMethod);

        TempData["SuccessMessage"] = fulfillmentMethod switch
        {
            FulfillmentMethod.Delivery => $"Order confirmed via Cash on Delivery. Pay ₱{finalPrice:N2} to the rider.",
            FulfillmentMethod.Halfway => $"Order confirmed via Cash. A chat has been opened to coordinate a meeting point.",
            FulfillmentMethod.Pickup => $"Order confirmed via Cash. A chat has been opened to arrange pickup.",
            _ => $"Order confirmed via Cash on Delivery."
        };

        if (fulfillmentMethod == FulfillmentMethod.Halfway || fulfillmentMethod == FulfillmentMethod.Pickup)
        {
            if (order.ChatConversationId.HasValue)
            {
                return RedirectToAction("Conversation", "Chat", new { area = "Mobile", id = order.ChatConversationId.Value });
            }
        }

        return RedirectToAction(nameof(MyPurchases));
    }

    // ── SHOP CHECKOUT / Option B (GET) ─────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> ShopCheckout(int skuId, int quantity = 1, FulfillmentMethod? fulfillmentMethod = null)
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
            return RedirectToAction("Details", "Shop", new { area = "Mobile", id = item.Id });
        }

        if (sku.Status != SkuStatus.Available || sku.Quantity <= 0)
        {
            TempData["ErrorMessage"] = "Sorry, this variant is out of stock.";
            return RedirectToAction("Details", "Shop", new { area = "Mobile", id = item.Id });
        }

        quantity = Math.Max(1, Math.Min(quantity, sku.Quantity));

        var wallet = await _walletService.GetOrCreateWalletAsync(buyerId.Value);
        var viewModel = BuildShopCheckoutViewModel(item, sku, quantity, wallet.Balance);

        if (fulfillmentMethod.HasValue)
        {
            viewModel.SelectedFulfillmentMethod = fulfillmentMethod.Value.ToString();
        }

        return View("Checkout", viewModel);
    }

    // ── CONFIRM SHOP ORDER / Option B (POST) ──────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmShopOrder(int skuId, int quantity, PaymentMethod paymentMethod, FulfillmentMethod fulfillmentMethod)
    {
        var buyerId = ResolveUserId();
        if (buyerId is null) return Unauthorized();

        if (!await HasCompleteProfileAsync(buyerId.Value))
            return RedirectToCompleteProfile("purchase items");

        quantity = Math.Max(1, quantity);

        var sku = await _context.ItemVariantSkus
            .Include(s => s.Variant)
                .ThenInclude(v => v.Item)
            .FirstOrDefaultAsync(s => s.Id == skuId);

        if (sku?.Variant?.Item is null) return NotFound();
        var item = sku.Variant.Item;

        if (item.UserId == buyerId.Value) return Forbid();

        if (!IsFulfillmentMethodAllowed(item, fulfillmentMethod))
        {
            TempData["ErrorMessage"] = $"The seller does not allow {fulfillmentMethod} for this item.";
            return RedirectToAction(nameof(ShopCheckout), new { skuId, quantity });
        }

        if (sku.Status != SkuStatus.Available || sku.Quantity < quantity)
        {
            int remaining = Math.Max(0, sku.Quantity);
            TempData["ErrorMessage"] = remaining == 0
                ? "Sorry, this item just sold out."
                : $"Only {remaining} unit(s) left.";

            return RedirectToAction(nameof(ShopCheckout),
                new { skuId, quantity = Math.Max(1, remaining) });
        }

        decimal itemPrice = sku.Price * quantity;
        decimal deliveryFee = fulfillmentMethod == FulfillmentMethod.Delivery
            ? ItemConstants.DeliveryFee
            : 0m;
        decimal finalPrice = itemPrice + deliveryFee;

        OrderStatus initialStatus = fulfillmentMethod == FulfillmentMethod.Delivery
            ? OrderStatus.Pending
            : OrderStatus.AwaitingMeeting;

        if (paymentMethod == PaymentMethod.Wallet)
        {
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
                    $"Insufficient wallet balance. You need ₱{escrowAmount:N2}. Please add funds or choose Cash on Delivery.";
                return RedirectToAction(nameof(ShopCheckout), new { skuId, quantity });
            }

            if (fulfillmentMethod == FulfillmentMethod.Delivery)
            {
                await _deliveryRepository.CreateForOrderAsync(tempOrder.Id);
            }
            else
            {
                await InitializeChatForOrderAsync(tempOrder);
                await _orderRepository.UpdateAsync(tempOrder);
            }

            sku.Quantity -= quantity;
            if (sku.Quantity == 0) sku.Status = SkuStatus.Sold;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Mobile: Shop Order {OrderId} (Wallet) — SKU {SkuId} ×{Qty}, ₱{FinalPrice}, Fulfillment: {FulfillmentMethod}.",
                tempOrder.Id, sku.Id, quantity, finalPrice, fulfillmentMethod);

            TempData["SuccessMessage"] = fulfillmentMethod switch
            {
                FulfillmentMethod.Delivery => $"Order confirmed! ₱{itemPrice:N2} held in escrow. Pay ₱{deliveryFee:N2} delivery fee to the rider in cash.",
                FulfillmentMethod.Halfway => $"Order confirmed! ₱{itemPrice:N2} held in escrow. A chat has been opened to coordinate a meeting point.",
                FulfillmentMethod.Pickup => $"Order confirmed! ₱{itemPrice:N2} held in escrow. A chat has been opened to arrange pickup.",
                _ => $"Order confirmed for ₱{finalPrice:N2}."
            };

            if (fulfillmentMethod == FulfillmentMethod.Halfway || fulfillmentMethod == FulfillmentMethod.Pickup)
            {
                if (tempOrder.ChatConversationId.HasValue)
                {
                    return RedirectToAction("Conversation", "Chat", new { area = "Mobile", id = tempOrder.ChatConversationId.Value });
                }
            }

            return RedirectToAction(nameof(MyPurchases));
        }

        // Cash on Delivery
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

        if (fulfillmentMethod == FulfillmentMethod.Delivery)
        {
            await _deliveryRepository.CreateForOrderAsync(order.Id);
        }
        else
        {
            await InitializeChatForOrderAsync(order);
            await _orderRepository.UpdateAsync(order);
        }

        sku.Quantity -= quantity;
        if (sku.Quantity == 0) sku.Status = SkuStatus.Sold;
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Mobile: Shop Order {OrderId} (COD) — SKU {SkuId} ×{Qty}, ₱{FinalPrice}, Fulfillment: {FulfillmentMethod}.",
            order.Id, sku.Id, quantity, finalPrice, fulfillmentMethod);

        TempData["SuccessMessage"] = fulfillmentMethod switch
        {
            FulfillmentMethod.Delivery => $"Order confirmed via Cash on Delivery. Pay ₱{finalPrice:N2} to the rider.",
            FulfillmentMethod.Halfway => $"Order confirmed via Cash. A chat has been opened to coordinate a meeting point.",
            FulfillmentMethod.Pickup => $"Order confirmed via Cash. A chat has been opened to arrange pickup.",
            _ => $"Order confirmed via Cash on Delivery."
        };

        if (fulfillmentMethod == FulfillmentMethod.Halfway || fulfillmentMethod == FulfillmentMethod.Pickup)
        {
            if (order.ChatConversationId.HasValue)
            {
                return RedirectToAction("Conversation", "Chat", new { area = "Mobile", id = order.ChatConversationId.Value });
            }
        }

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
            return RedirectToAction("Index", "Cart", new { area = "Mobile" });
        }

        var cartItems = await _context.CartItems
            .Include(ci => ci.Item).ThenInclude(i => i!.Shop)
            .Include(ci => ci.ItemVariantSku).ThenInclude(s => s!.Variant)
            .Where(ci => cartItemIds.Contains(ci.Id) && ci.UserId == buyerId.Value)
            .ToListAsync();

        if (cartItems.Count == 0)
        {
            TempData["ErrorMessage"] = "Selected items not found in your cart.";
            return RedirectToAction("Index", "Cart", new { area = "Mobile" });
        }

        var outOfStock = cartItems.Where(ci => ci.ItemVariantSku!.Quantity < ci.Quantity).ToList();
        if (outOfStock.Count > 0)
        {
            TempData["ErrorMessage"] = "Some items are out of stock.";
            return RedirectToAction("Index", "Cart", new { area = "Mobile" });
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
            return RedirectToAction("Index", "Cart", new { area = "Mobile" });
        }

        var cartItems = await _context.CartItems
            .Include(ci => ci.Item).ThenInclude(i => i!.Shop)
            .Include(ci => ci.ItemVariantSku).ThenInclude(s => s!.Variant)
            .Where(ci => cartItemIds.Contains(ci.Id) && ci.UserId == buyerId.Value)
            .ToListAsync();

        if (cartItems.Count == 0)
        {
            TempData["ErrorMessage"] = "Selected items not found in your cart.";
            return RedirectToAction("Index", "Cart", new { area = "Mobile" });
        }

        var isWalletPayment = paymentMethod == "wallet";
        var wallet = await _walletService.GetOrCreateWalletAsync(buyerId.Value);
        var createdOrderIds = new List<int>();

        var groupedBySeller = cartItems.GroupBy(ci => ci.Item!.UserId).ToList();

        decimal subtotal = cartItems.Sum(ci => ci.ItemVariantSku!.Price * ci.Quantity);
        decimal totalDeliveryFee = groupedBySeller.Count * ItemConstants.DeliveryFee;
        decimal grandTotal = subtotal + totalDeliveryFee;

        if (isWalletPayment && wallet.Balance < grandTotal)
        {
            TempData["ErrorMessage"] =
                $"Insufficient wallet balance. You need ₱{grandTotal:N2} but your balance is ₱{wallet.Balance:N2}.";
            return RedirectToAction(nameof(CartCheckout), new { cartItemIds });
        }

        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var sellerGroup in groupedBySeller)
                {
                    var sellerId = sellerGroup.Key;
                    var sellerItems = sellerGroup.ToList();

                    decimal sellerSubtotal = sellerItems.Sum(ci => ci.ItemVariantSku!.Price * ci.Quantity);
                    decimal sellerFinalPrice = sellerSubtotal + ItemConstants.DeliveryFee;

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
                        FulfillmentMethod = FulfillmentMethod.Delivery,
                        CashCollectedByRider = false
                    };

                    order.ItemId = sellerItems.First().ItemId;

                    await _orderRepository.AddAsync(order);
                    await _context.SaveChangesAsync();

                    foreach (var ci in sellerItems)
                    {
                        var sku = ci.ItemVariantSku!;
                        var item = ci.Item!;
                        var qty = ci.Quantity;

                        if (sku.Quantity < qty)
                            throw new InvalidOperationException($"Item '{item.Title}' is now out of stock.");

                        var orderItem = new OrderItem
                        {
                            OrderId = order.Id,
                            ItemVariantSkuId = sku.Id,
                            Quantity = qty,
                            UnitPrice = sku.Price
                        };
                        _context.OrderItems.Add(orderItem);

                        sku.Quantity -= qty;
                        if (sku.Quantity == 0) sku.Status = SkuStatus.Sold;

                        _context.CartItems.Remove(ci);
                    }

                    await _context.SaveChangesAsync();

                    if (isWalletPayment)
                    {
                        bool held = await _walletService.HoldEscrowAsync(order.Id, buyerId.Value, sellerFinalPrice);
                        if (!held)
                        {
                            throw new InvalidOperationException($"Wallet deduction failed for order from seller {sellerId}.");
                        }
                    }

                    await _deliveryRepository.CreateForOrderAsync(order.Id);

                    createdOrderIds.Add(order.Id);
                }

                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Mobile: Cart Checkout — Buyer {BuyerId} created {OrderCount} orders from {ItemCount} items.",
                    buyerId.Value, createdOrderIds.Count, cartItems.Count);

                TempData["SuccessMessage"] =
                    $"Order{(createdOrderIds.Count > 1 ? "s" : "")} confirmed for {cartItems.Count} item{(cartItems.Count > 1 ? "s" : "")} " +
                    $"from {groupedBySeller.Count} seller{(groupedBySeller.Count > 1 ? "s" : "")}. Total: ₱{grandTotal:N2}.";

                return RedirectToAction(nameof(MyPurchases));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Mobile: Cart checkout failed for Buyer {BuyerId}", buyerId.Value);
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Index", "Cart", new { area = "Mobile" });
            }
        });
    }

    // ── MARK DELIVERED ────────────────────────────────────────────────────

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

        if (order.FulfillmentMethod != FulfillmentMethod.Delivery)
        {
            return await MarkNonDeliveryOrderCompleted(order, buyerId.Value);
        }

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
            TempData["ErrorMessage"] = "No rider assigned to this delivery.";
            return RedirectToAction(nameof(MyPurchases));
        }

        decimal itemPrice = order.FinalPrice - order.DeliveryFee;
        decimal deliveryFee = order.DeliveryFee;

        if (order.PaymentMethod == PaymentMethod.Wallet)
        {
            await _walletService.ReleaseEscrowAsync(order.Id, order.BuyerId, order.SellerId, itemPrice);
            await _walletService.PayRiderAsync(order.Id, order.BuyerId, riderUserId.Value, deliveryFee, fromEscrow: true);
        }
        else if (order.PaymentMethod == PaymentMethod.Cash && !order.CashCollectedByRider)
        {
            await _walletService.RecordCashCollectionAsync(order.Id, order.BuyerId, order.SellerId, itemPrice);
            await _walletService.PayRiderAsync(order.Id, order.BuyerId, riderUserId.Value, deliveryFee, fromEscrow: false);
        }

        var confirmed = await _deliveryRepository.ConfirmByBuyerAsync(delivery.Id, buyerId.Value);

        if (!confirmed)
        {
            TempData["ErrorMessage"] = "Failed to confirm delivery. Please try again.";
            return RedirectToAction(nameof(MyPurchases));
        }

        _logger.LogInformation(
            "Mobile: Order {OrderId} marked Completed by Buyer {BuyerId}.",
            orderId, buyerId.Value);

        TempData["SuccessMessage"] =
            $"Delivery confirmed! Seller paid ₱{itemPrice:N2}, rider received ₱{deliveryFee:N2}. Thank you!";

        return RedirectToAction(nameof(MyPurchases));
    }

    // ── MARK NON-DELIVERY ORDER COMPLETED ───────────────────────────────────

    private async Task<IActionResult> MarkNonDeliveryOrderCompleted(Order order, int buyerId)
    {
        decimal itemPrice = order.FinalPrice - order.DeliveryFee;

        if (order.PaymentMethod == PaymentMethod.Wallet)
        {
            await _walletService.ReleaseEscrowAsync(order.Id, order.BuyerId, order.SellerId, itemPrice);
        }
        else if (order.PaymentMethod == PaymentMethod.Cash && !order.CashCollectedByRider)
        {
            await _walletService.RecordCashCollectionAsync(order.Id, order.BuyerId, order.SellerId, itemPrice);
            order.CashCollectedByRider = true;
        }

        order.Status = OrderStatus.Completed;
        await _orderRepository.UpdateAsync(order);

        if (order.ChatConversationId.HasValue)
        {
            try
            {
                await _chatService.SendOrderConfirmationMessageAsync(
                    order.ChatConversationId.Value, order.Id, 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Mobile: Failed to send completion message for Order {OrderId}", order.Id);
            }
        }

        _logger.LogInformation(
            "Mobile: Non-delivery Order {OrderId} marked Completed. Fulfillment: {FulfillmentMethod}.",
            order.Id, order.FulfillmentMethod);

        TempData["SuccessMessage"] = $"Order confirmed! Seller paid ₱{itemPrice:N2}. Thank you!";

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
            "Mobile: User {BuyerId} viewed My Purchases ({Count} orders).", buyerId.Value, orders.Count);

        return View(orders);
    }

    // ── Private helpers ────────────────────────────────────────────────────

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

        string defaultMethod = item.AllowDelivery ? "Delivery"
            : item.AllowHalfway ? "Halfway"
            : item.AllowPickup ? "Pickup"
            : "Delivery";

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
            AllowPickup = item.AllowPickup,
            SelectedFulfillmentMethod = defaultMethod
        };
    }

    private static CheckoutViewModel BuildShopCheckoutViewModel(
        Item item, ItemVariantSku sku, int quantity, decimal buyerBalance)
    {
        string sellerEmail = item.User?.Email ?? string.Empty;
        string sellerDisplay = sellerEmail.Contains('@')
            ? sellerEmail.Split('@')[0]
            : sellerEmail;

        string defaultMethod = item.AllowDelivery ? "Delivery"
            : item.AllowHalfway ? "Halfway"
            : item.AllowPickup ? "Pickup"
            : "Delivery";

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
            AllowPickup = item.AllowPickup,
            SelectedFulfillmentMethod = defaultMethod
        };
    }

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