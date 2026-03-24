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
    private readonly IWalletService _walletService;
    private readonly IOrderService _orderService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        IItemRepository itemRepository,
        IOrderRepository orderRepository,
        IWalletService walletService,
        IOrderService orderService,
        ApplicationDbContext context,
        ILogger<OrdersController> logger)
    {
        _itemRepository = itemRepository;
        _orderRepository = orderRepository;
        _walletService = walletService;
        _orderService = orderService;
        _context = context;
        _logger = logger;
    }

    // ── P2P CHECKOUT (GET) ─────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Checkout(int itemId)
    {
        var buyerId = ResolveUserId();
        if (buyerId is null) return Unauthorized();

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
    public async Task<IActionResult> ConfirmOrder(int itemId, PaymentMethod paymentMethod)
    {
        var buyerId = ResolveUserId();
        if (buyerId is null) return Unauthorized();

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

        decimal finalPrice = item.Price;

        // ── Wallet payment: hold funds in escrow ───────────────────────────
        if (paymentMethod == PaymentMethod.Wallet)
        {
            var tempOrder = new Order
            {
                ItemId = item.Id,
                BuyerId = buyerId.Value,
                SellerId = item.UserId,
                FinalPrice = finalPrice,
                OrderDate = DateTime.UtcNow,
                Status = OrderStatus.Pending,
                PaymentMethod = PaymentMethod.Wallet
            };
            await _orderRepository.AddAsync(tempOrder);

            bool held = await _walletService.HoldEscrowAsync(tempOrder.Id, buyerId.Value, finalPrice);
            if (!held)
            {
                await _orderRepository.DeleteAsync(tempOrder.Id);
                TempData["ErrorMessage"] =
                    $"Insufficient wallet balance. You need ₱{finalPrice:N2} but your available balance is too low. " +
                    "Please add funds or choose Cash on Delivery.";
                return RedirectToAction(nameof(Checkout), new { itemId });
            }

            item.Status = ItemStatus.Sold;
            await _itemRepository.UpdateAsync(item);

            _logger.LogInformation(
                "Order {OrderId} created (Wallet) — Item {ItemId}, Buyer {BuyerId}, Seller {SellerId}, ₱{FinalPrice}.",
                tempOrder.Id, item.Id, buyerId.Value, item.UserId, finalPrice);

            TempData["SuccessMessage"] =
                $"Order confirmed for '{item.Title}' at ₱{finalPrice:N2}. " +
                $"₱{finalPrice:N2} is held in escrow and will be released to the seller once you confirm delivery.";

            return RedirectToAction(nameof(MyPurchases));
        }

        // ── Cash on Delivery ───────────────────────────────────────────────
        var order = new Order
        {
            ItemId = item.Id,
            BuyerId = buyerId.Value,
            SellerId = item.UserId,
            FinalPrice = finalPrice,
            OrderDate = DateTime.UtcNow,
            Status = OrderStatus.Pending,
            PaymentMethod = PaymentMethod.Cash,
            CashCollectedByRider = false
        };

        await _orderRepository.AddAsync(order);

        item.Status = ItemStatus.Sold;
        await _itemRepository.UpdateAsync(item);

        _logger.LogInformation(
            "Order {OrderId} created (COD) — Item {ItemId}, Buyer {BuyerId}, Seller {SellerId}, ₱{FinalPrice}.",
            order.Id, item.Id, buyerId.Value, item.UserId, finalPrice);

        TempData["SuccessMessage"] =
            $"Order confirmed for '{item.Title}' at ₱{finalPrice:N2} via Cash on Delivery. " +
            "Please coordinate with the seller for delivery details.";

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
    public async Task<IActionResult> ConfirmShopOrder(int skuId, int quantity, PaymentMethod paymentMethod)
    {
        var buyerId = ResolveUserId();
        if (buyerId is null) return Unauthorized();

        quantity = Math.Max(1, quantity);

        // Load tracked so we can decrement stock in the same SaveChanges call
        var sku = await _context.ItemVariantSkus
            .Include(s => s.Variant)
                .ThenInclude(v => v.Item)
            .FirstOrDefaultAsync(s => s.Id == skuId);

        if (sku?.Variant?.Item is null) return NotFound();
        var item = sku.Variant.Item;

        if (item.UserId == buyerId.Value) return Forbid();

        // ── First-come-first-served stock check ────────────────────────────
        // Re-read stock at confirmation time; the GET-time value may be stale.
        if (sku.Status != SkuStatus.Available || sku.Quantity < quantity)
        {
            int remaining = Math.Max(0, sku.Quantity);
            TempData["ErrorMessage"] = remaining == 0
                ? "Sorry, this item just sold out while you were checking out."
                : $"Only {remaining} unit(s) left. Please reduce your quantity.";

            // Bounce back to checkout with corrected quantity
            return RedirectToAction(nameof(ShopCheckout),
                new { skuId, quantity = Math.Max(1, remaining) });
        }

        decimal finalPrice = sku.Price * quantity;

        // ── Wallet path: create order → hold escrow → decrement stock ──────
        if (paymentMethod == PaymentMethod.Wallet)
        {
            var tempOrder = new Order
            {
                ItemId = item.Id,
                ItemVariantSkuId = sku.Id,
                BuyerId = buyerId.Value,
                SellerId = item.UserId,
                FinalPrice = finalPrice,
                Quantity = quantity,
                OrderDate = DateTime.UtcNow,
                Status = OrderStatus.Pending,
                PaymentMethod = PaymentMethod.Wallet
            };
            await _orderRepository.AddAsync(tempOrder);

            bool held = await _walletService.HoldEscrowAsync(tempOrder.Id, buyerId.Value, finalPrice);
            if (!held)
            {
                await _orderRepository.DeleteAsync(tempOrder.Id);
                TempData["ErrorMessage"] =
                    $"Insufficient wallet balance. You need ₱{finalPrice:N2}. " +
                    "Please add funds or choose Cash on Delivery.";
                return RedirectToAction(nameof(ShopCheckout), new { skuId, quantity });
            }

            // Decrement stock — if it hits zero, mark the SKU sold
            sku.Quantity -= quantity;
            if (sku.Quantity == 0) sku.Status = SkuStatus.Sold;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Shop Order {OrderId} (Wallet) — SKU {SkuId} ×{Qty}, Buyer {BuyerId}, " +
                "Seller {SellerId}, ₱{FinalPrice}.",
                tempOrder.Id, sku.Id, quantity, buyerId.Value, item.UserId, finalPrice);

            TempData["SuccessMessage"] =
                $"Order confirmed for '{item.Title}' ×{quantity} at ₱{finalPrice:N2}. " +
                "Funds are held in escrow and will be released once you confirm delivery.";

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
            Quantity = quantity,
            OrderDate = DateTime.UtcNow,
            Status = OrderStatus.Pending,
            PaymentMethod = PaymentMethod.Cash,
            CashCollectedByRider = false
        };
        await _orderRepository.AddAsync(order);

        sku.Quantity -= quantity;
        if (sku.Quantity == 0) sku.Status = SkuStatus.Sold;
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Shop Order {OrderId} (COD) — SKU {SkuId} ×{Qty}, Buyer {BuyerId}, " +
            "Seller {SellerId}, ₱{FinalPrice}.",
            order.Id, sku.Id, quantity, buyerId.Value, item.UserId, finalPrice);

        TempData["SuccessMessage"] =
            $"Order confirmed for '{item.Title}' ×{quantity} at ₱{finalPrice:N2} via Cash on Delivery. " +
            "Please coordinate with the seller for delivery details.";

        return RedirectToAction(nameof(MyPurchases));
    }

    // ── MARK DELIVERED ─────────────────────────────────────────────────────

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

        if (order.PaymentMethod == PaymentMethod.Wallet)
        {
            await _walletService.ReleaseEscrowAsync(
                order.Id, order.BuyerId, order.SellerId, order.FinalPrice);
        }
        else if (order.PaymentMethod == PaymentMethod.Cash && !order.CashCollectedByRider)
        {
            await _walletService.RecordCashCollectionAsync(
                order.Id, order.BuyerId, order.SellerId, order.FinalPrice);
            order.CashCollectedByRider = true;
        }

        order.Status = OrderStatus.Completed;
        await _orderRepository.UpdateAsync(order);

        _logger.LogInformation(
            "Order {OrderId} marked Completed by Buyer {BuyerId}.", orderId, buyerId.Value);

        TempData["SuccessMessage"] = "Delivery confirmed. The seller has been paid. Thank you!";
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
            BuyerBalance = buyerBalance
            // IsShopOrder defaults to false; SkuId / Quantity / MaxQuantity keep their defaults
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
            MaxQuantity = sku.Quantity, // stock at GET time — used to cap the stepper
            SelectedVariantName = sku.Variant?.Name ?? string.Empty,
            SelectedSize = sku.Size,
            BuyerBalance = buyerBalance,
            WasStolen = false,
            IsStealable = false
        };
    }
}