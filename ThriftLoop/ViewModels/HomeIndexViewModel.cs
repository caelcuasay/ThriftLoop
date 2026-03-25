using ThriftLoop.Models;

namespace ThriftLoop.ViewModels;

/// <summary>
/// ViewModel for the public "For You" home feed.
/// CurrentUserId is null when the visitor is anonymous; the view uses it
/// to suppress "Add to Cart" / "Buy Now" on items the viewer owns.
/// </summary>
public class HomeIndexViewModel
{
    public IReadOnlyList<Item> Items { get; init; } = [];

    /// <summary>
    /// The authenticated user's ID, or null if the request is anonymous.
    /// </summary>
    public int? CurrentUserId { get; init; }

    /// <summary>
    /// Price display info for shop items (min-max range)
    /// Key: Item Id, Value: formatted price string (e.g., "₱500 - ₱1,200" or just "₱500")
    /// </summary>
    public Dictionary<int, string> ShopItemPriceDisplay { get; init; } = new();
}