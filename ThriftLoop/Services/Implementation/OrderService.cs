using ThriftLoop.Enums;
using ThriftLoop.Models;
using ThriftLoop.Services.OrderManagement.Interface;

namespace ThriftLoop.Services.OrderManagement.Implementation;

/// <inheritdoc />
public class OrderService : IOrderService
{
    /// <inheritdoc />
    public (bool allowed, string reason) CanBuyerCheckout(Item item, int buyerId)
    {
        if (item.ListingType == ListingType.Stealable)
            return CheckStealableListing(item, buyerId);

        return CheckStandardListing(item);
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private static (bool allowed, string reason) CheckStealableListing(Item item, int buyerId)
    {
        return item.Status switch
        {
            // Stealer has bumped the price and must complete checkout immediately.
            ItemStatus.StolenPendingCheckout =>
                item.CurrentWinnerId == buyerId
                    ? (true, string.Empty)
                    : (false, "You are not the buyer for this stolen item."),

            // Order already exists — only the winner can re-enter checkout.
            ItemStatus.Sold =>
                item.CurrentWinnerId == buyerId
                    ? (true, string.Empty)
                    : (false, "You are not the buyer for this item."),

            // Original getter is in (or waiting for) their finalize window.
            ItemStatus.Reserved => CheckReservedStealable(item, buyerId),

            _ => (false, "This item is not ready for checkout. Please claim it first.")
        };
    }

    private static (bool allowed, string reason) CheckReservedStealable(Item item, int buyerId)
    {
        if (item.CurrentWinnerId != buyerId)
            return (false, "You are not the current winner of this item.");

        if (!item.IsInFinalizeWindow)
            return (false, "The purchase window for this item has not opened yet, or has expired.");

        return (true, string.Empty);
    }

    private static (bool allowed, string reason) CheckStandardListing(Item item)
    {
        return item.Status switch
        {
            ItemStatus.Sold => (false, "This item has already been sold."),
            ItemStatus.Reserved => (false, "This item is currently reserved by another buyer."),
            _ => (true, string.Empty)
        };
    }
}