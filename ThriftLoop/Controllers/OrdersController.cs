using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;
using ThriftLoop.ViewModels;

namespace ThriftLoop.Controllers;

[Authorize]
public class OrdersController : Controller
{
    private readonly IItemRepository _itemRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<OrdersController> _logger;

    private const decimal StealPremium = 50m;

    public OrdersController(
        IItemRepository itemRepository,
        IOrderRepository orderRepository,
        ILogger<OrdersController> logger)
    {
        _itemRepository = itemRepository;
        _orderRepository = orderRepository;
        _logger = logger;
    }

    // ── CHECKOUT ───────────────────────────────────────────────────────────

    /// <summary>
    /// GET /Orders/Checkout?itemId={id}
    /// Renders the order summary / confirm-purchase page.
    ///
    /// Access rules
    /// ─────────────
    /// Standard listing  — any authenticated non-owner buyer (future flow).
    /// Stealable Sold    — only the buyer who just stole (CurrentWinnerId).
    /// Stealable Reserved (in finalize window) — only the original "Get" winner.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Checkout(int itemId)
    {
        var buyerId = ResolveUserId();
        if (buyerId is null) return Unauthorized();

        var item = await _itemRepository.GetByIdWithUserAsync(itemId);
        if (item is null) return NotFound();

        // ── Guard: seller cannot buy their own item ───────────────────────
        if (item.UserId == buyerId.Value)
        {
            TempData["ErrorMessage"] = "You cannot purchase your own listing.";
            return RedirectToAction("Details", "Items", new { id = itemId });
        }

        // ── Guard: item must be in a purchasable state for this buyer ─────
        var (allowed, reason) = IsBuyerAllowedToCheckout(item, buyerId.Value);
        if (!allowed)
        {
            TempData["ErrorMessage"] = reason;
            return RedirectToAction("Details", "Items", new { id = itemId });
        }

        // ── Guard: already has an order (double-submit protection) ────────
        var existingOrder = await _orderRepository.GetOrderByItemIdAsync(itemId);
        if (existingOrder is not null)
        {
            _logger.LogWarning(
                "User {BuyerId} attempted to re-checkout Item {ItemId} which already has Order {OrderId}.",
                buyerId.Value, itemId, existingOrder.Id);
            TempData["InfoMessage"] = "This item has already been purchased.";
            return RedirectToAction(nameof(MyPurchases));
        }

        var viewModel = BuildCheckoutViewModel(item, buyerId.Value);
        return View(viewModel);
    }

    // ── CONFIRM ORDER ──────────────────────────────────────────────────────

    /// <summary>
    /// POST /Orders/ConfirmOrder
    /// Creates the Order record, marks the item as Sold, and redirects to
    /// the My Purchases page with a success message.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmOrder(int itemId)
    {
        var buyerId = ResolveUserId();
        if (buyerId is null) return Unauthorized();

        // Use GetByIdAsync (no User included) so EF's Update() does NOT
        // traverse into the User navigation property and issue a spurious
        // UPDATE on the seller's row alongside the Item update.
        var item = await _itemRepository.GetByIdAsync(itemId);
        if (item is null) return NotFound();

        // ── Guard: seller cannot self-purchase ────────────────────────────
        if (item.UserId == buyerId.Value)
            return Forbid();

        // ── Guard: buyer must still be eligible ───────────────────────────
        var (allowed, reason) = IsBuyerAllowedToCheckout(item, buyerId.Value);
        if (!allowed)
        {
            TempData["ErrorMessage"] = reason;
            return RedirectToAction("Details", "Items", new { id = itemId });
        }

        // ── Guard: idempotency — prevent double-submit ────────────────────
        var existingOrder = await _orderRepository.GetOrderByItemIdAsync(itemId);
        if (existingOrder is not null)
        {
            TempData["InfoMessage"] = "Your order was already confirmed.";
            return RedirectToAction(nameof(MyPurchases));
        }

        // ── Determine the final price ─────────────────────────────────────
        // item.Price already includes the ₱50 steal premium if the item was
        // stolen (StealItem adds it before calling UpdateAsync).
        decimal finalPrice = item.Price;

        // ── Create the order ──────────────────────────────────────────────
        var order = new Order
        {
            ItemId = item.Id,
            BuyerId = buyerId.Value,
            SellerId = item.UserId,
            FinalPrice = finalPrice,
            OrderDate = DateTime.UtcNow,
            Status = OrderStatus.Pending
        };

        await _orderRepository.AddAsync(order);

        // ── Mark item as Sold (finalize path: Reserved → Sold) ────────────
        // For the steal path the item is already Sold, so this is idempotent.
        if (item.Status != ItemStatus.Sold)
        {
            item.Status = ItemStatus.Sold;
            await _itemRepository.UpdateAsync(item);
        }

        _logger.LogInformation(
            "Order {OrderId} created — Item {ItemId}, Buyer {BuyerId}, Seller {SellerId}, " +
            "FinalPrice ₱{FinalPrice}.",
            order.Id, item.Id, buyerId.Value, item.UserId, finalPrice);

        TempData["SuccessMessage"] =
            $"Your order for '{item.Title}' has been confirmed at ₱{finalPrice:N2}. " +
            $"Please coordinate with the seller for payment and delivery.";

        return RedirectToAction(nameof(MyPurchases));
    }

    // ── MY PURCHASES ───────────────────────────────────────────────────────

    /// <summary>
    /// GET /Orders/MyPurchases
    /// Displays all orders placed by the current authenticated user.
    /// </summary>
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
    /// Reads the NameIdentifier claim and returns the user ID, or null if
    /// the claim is absent or unparseable (should not happen for [Authorize]
    /// actions, but guards against misconfigured middleware).
    /// </summary>
    private int? ResolveUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out int id) ? id : null;
    }

    /// <summary>
    /// Determines whether the given buyer is allowed to proceed to checkout
    /// for the given item, and returns a human-readable reason when not.
    ///
    /// Rules
    /// ─────
    /// Stealable Sold    — CurrentWinnerId must equal the buyer (steal path).
    /// Stealable Reserved — CurrentWinnerId must equal the buyer AND the item
    ///                     must be inside its 2-hour finalize window.
    /// Standard Available — any authenticated non-owner buyer may proceed.
    /// Anything Sold already with an order — blocked (handled separately).
    /// </summary>
    private static (bool allowed, string reason) IsBuyerAllowedToCheckout(Item item, int buyerId)
    {
        if (item.ListingType == ListingType.Stealable)
        {
            // Steal path: item was already marked Sold by StealItem action.
            if (item.Status == ItemStatus.Sold)
            {
                if (item.CurrentWinnerId != buyerId)
                    return (false, "You are not the buyer for this item.");

                return (true, string.Empty);
            }

            // Finalize path: item is still Reserved after steal window expired.
            if (item.Status == ItemStatus.Reserved)
            {
                if (item.CurrentWinnerId != buyerId)
                    return (false, "You are not the current winner of this item.");

                if (!item.IsInFinalizeWindow)
                    return (false, "The purchase window for this item has expired.");

                return (true, string.Empty);
            }

            // Available (no one has claimed it yet) or any other state.
            return (false, "This item is not ready for checkout. Please claim it first.");
        }

        // Standard listing — item must be Available (not already sold/reserved).
        if (item.Status == ItemStatus.Sold)
            return (false, "This item has already been sold.");

        if (item.Status == ItemStatus.Reserved)
            return (false, "This item is currently reserved by another buyer.");

        return (true, string.Empty);
    }

    /// <summary>
    /// Builds the CheckoutViewModel from a fully-loaded Item (with .User) and
    /// the resolved buyer ID. Derives WasStolen and the price breakdown without
    /// requiring an extra database column — the steal state is inferred from
    /// the item's current Status and CurrentWinnerId.
    /// </summary>
    private static CheckoutViewModel BuildCheckoutViewModel(Item item, int buyerId)
    {
        // A steal sets item.Status = Sold immediately. A regular finalize leaves
        // the item as Reserved until ConfirmOrder fires. Use that distinction to
        // reconstruct the price breakdown.
        bool wasStolen = item.ListingType == ListingType.Stealable
                      && item.Status == ItemStatus.Sold
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
            IsStealable = item.ListingType == ListingType.Stealable
        };
    }
}