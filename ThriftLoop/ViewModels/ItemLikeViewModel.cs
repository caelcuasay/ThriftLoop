using ThriftLoop.Enums;

namespace ThriftLoop.ViewModels;

/// <summary>
/// ViewModel for displaying a liked item.
/// </summary>
public class LikedItemViewModel
{
    public int ItemId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal Price { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public ItemStatus Status { get; set; }

    // Seller info
    public int SellerId { get; set; }
    public string SellerName { get; set; } = string.Empty;

    // Shop info (null for P2P items)
    public int? ShopId { get; set; }
    public string? ShopName { get; set; }

    // Like info
    public DateTime LikedAt { get; set; }
    public int LikeCount { get; set; }

    // Computed
    public bool IsAvailable => Status == ItemStatus.Available;
    public bool IsP2P => ShopId == null;
}

/// <summary>
/// ViewModel for the liked items page.
/// </summary>
public class LikedItemsViewModel
{
    public List<LikedItemViewModel> Items { get; set; } = new();
    public bool IsEmpty => Items.Count == 0;

    // Filter options
    public string? CurrentFilter { get; set; }
}

/// <summary>
/// DTO for toggling a like via AJAX.
/// </summary>
public class ToggleLikeDto
{
    public int ItemId { get; set; }
}

/// <summary>
/// Response for like toggle AJAX call.
/// </summary>
public class LikeToggleResponse
{
    public bool Success { get; set; }
    public bool IsLiked { get; set; }
    public int LikeCount { get; set; }
    public string? Error { get; set; }
}
