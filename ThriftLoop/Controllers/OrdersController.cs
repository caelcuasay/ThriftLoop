using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;
using ThriftLoop.Services.WalletManagement.Interface;
using ThriftLoop.ViewModels;

namespace ThriftLoop.Controllers;

[Authorize]
public class OrdersController : Controller
{
    private readonly IItemRepository _itemRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IWalletService _walletService;
    private readonly ILogger<OrdersController> _logger;

    private const decimal StealPremium = 50m;

    public OrdersController(
        IItemRepository itemRepository,
        IOrderRepository orderRepository,
        IWalletService walletService,
        ILogger<OrdersController> logger)
    {
        _itemRepository = itemRepository;
        _orderRepository = orderRepository;
        _walletService = walletService;
        _logger = logger;
    }

    // ── CHECKOUT ───────────────────────────────────────────────────────────

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

        var (allowed, reason) = IsBuyerAllowedToCheckout(item, buyerId.Value);
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
        var viewModel = BuildCheckoutViewModel(item, buyerId.Value, wallet.Balance);
        return View(viewModel);
    }

    // ── CONFIRM ORDER ──────────────────────────────────────────────────────

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

        var (allowed, reason) = IsBuyerAllowedToCheckout(item, buyerId.Value);
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

            // Mark Sold now that the order is confirmed and funds are held.
            item.Status = ItemStatus.Sold;
            await _itemRepository.UpdateAsync(item);

            _logger.LogInformation(
                "Order {OrderId} created (Wallet) — Item {ItemId}, Buyer {BuyerId}, " +
                "Seller {SellerId}, ₱{FinalPrice}.",
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

        // Mark Sold now that the order is confirmed.
        item.Status = ItemStatus.Sold;
        await _itemRepository.UpdateAsync(item);

        _logger.LogInformation(
            "Order {OrderId} created (COD) — Item {ItemId}, Buyer {BuyerId}, " +
            "Seller {SellerId}, ₱{FinalPrice}.",
            order.Id, item.Id, buyerId.Value, item.UserId, finalPrice);

        TempData["SuccessMessage"] =
            $"Order confirmed for '{item.Title}' at ₱{finalPrice:N2} via Cash on Delivery. " +
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

    private int? ResolveUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out int id) ? id : null;
    }

    private static (bool allowed, string reason) IsBuyerAllowedToCheckout(Item item, int buyerId)
    {
        if (item.ListingType == ListingType.Stealable)
        {
            // ── Steal pending checkout ─────────────────────────────────────
            // Stealer has bumped the price and needs to complete the order.
            // Allow immediately — no finalize window required.
            if (item.Status == ItemStatus.StolenPendingCheckout)
            {
                if (item.CurrentWinnerId != buyerId)
                    return (false, "You are not the buyer for this stolen item.");
                return (true, string.Empty);
            }

            // ── Already sold (order exists) ────────────────────────────────
            if (item.Status == ItemStatus.Sold)
            {
                if (item.CurrentWinnerId != buyerId)
                    return (false, "You are not the buyer for this item.");
                return (true, string.Empty);
            }

            // ── Reserved — original getter in finalize window ──────────────
            if (item.Status == ItemStatus.Reserved)
            {
                if (item.CurrentWinnerId != buyerId)
                    return (false, "You are not the current winner of this item.");
                if (!item.IsInFinalizeWindow)
                    return (false, "The purchase window for this item has not opened yet, or has expired.");
                return (true, string.Empty);
            }

            return (false, "This item is not ready for checkout. Please claim it first.");
        }

        // ── Standard listing ───────────────────────────────────────────────
        if (item.Status == ItemStatus.Sold)
            return (false, "This item has already been sold.");
        if (item.Status == ItemStatus.Reserved)
            return (false, "This item is currently reserved by another buyer.");

        return (true, string.Empty);
    }

    private static CheckoutViewModel BuildCheckoutViewModel(Item item, int buyerId, decimal buyerBalance)
    {
        // wasStolen: the buyer is checking out a steal — item is StolenPendingCheckout
        // and they are the current winner who applied the ₱50 premium.
        bool wasStolen = item.ListingType == ListingType.Stealable
                      && item.Status == ItemStatus.StolenPendingCheckout
                      && item.CurrentWinnerId == buyerId;

        decimal basePrice = wasStolen ? item.Price - StealPremium : item.Price;
        decimal stealPremium = wasStolen ? StealPremium : 0m;

        string sellerEmail = item.User?.Email ?? string.Empty;
        string sellerDisplay = sellerEmail.Contains('@') ? sellerEmail.Split('@')[0] : sellerEmail;

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
            StealPremium = stealPremium,
            WasStolen = wasStolen,
            IsStealable = item.ListingType == ListingType.Stealable,
            BuyerBalance = buyerBalance
        };
    }
}