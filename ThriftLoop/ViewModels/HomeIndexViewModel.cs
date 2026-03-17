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
}