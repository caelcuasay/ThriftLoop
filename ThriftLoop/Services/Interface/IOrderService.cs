using ThriftLoop.Models;

namespace ThriftLoop.Services.OrderManagement.Interface;

/// <summary>
/// Encapsulates business rules for order eligibility and checkout validation.
/// </summary>
public interface IOrderService
{
    /// <summary>
    /// Determines whether a buyer is allowed to proceed to checkout for a given item.
    /// Handles both Standard and Stealable listing rules, including finalize windows
    /// and stolen-pending-checkout states.
    /// </summary>
    /// <param name="item">The item being purchased. Must have navigation properties loaded if needed.</param>
    /// <param name="buyerId">The authenticated buyer's user ID.</param>
    /// <returns>
    /// A tuple where <c>allowed</c> is <c>true</c> if checkout can proceed,
    /// and <c>reason</c> contains the user-facing error message if not.
    /// </returns>
    (bool allowed, string reason) CanBuyerCheckout(Item item, int buyerId);
}