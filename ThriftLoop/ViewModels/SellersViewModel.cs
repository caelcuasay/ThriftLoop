using ThriftLoop.Models;

namespace ThriftLoop.ViewModels;

/// <summary>
/// Drives the Sellers discovery page.
/// Items contains only shop listings (ShopId != null).
/// Shops is the horizontal suggestion row at the top.
/// </summary>
public class SellersViewModel
{
    /// <summary>All non-sold shop items, newest first.</summary>
    public IReadOnlyList<Item> Items { get; init; } = [];

    /// <summary>All approved SellerProfiles for the shop suggestion row.</summary>
    public IReadOnlyList<SellerProfile> Shops { get; init; } = [];

    /// <summary>The authenticated user's ID, or null if anonymous.</summary>
    public int? CurrentUserId { get; init; }

    /// <summary>
    /// Price display info for shop items (min-max range)
    /// Key: Item Id, Value: formatted price string (e.g., "₱500 - ₱1,200" or just "₱500")
    /// </summary>
    public Dictionary<int, string> ShopItemPriceDisplay { get; init; } = new();

    /// <summary>
    /// Set of item IDs that the current user has liked.
    /// Empty for anonymous users.
    /// </summary>
    public HashSet<int> LikedItemIds { get; init; } = new();

    /// <summary>
    /// Like counts per item.
    /// Key: Item Id, Value: number of likes.
    /// </summary>
    public Dictionary<int, int> LikeCounts { get; init; } = new();
}