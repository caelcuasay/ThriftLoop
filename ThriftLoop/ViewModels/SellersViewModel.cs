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
}