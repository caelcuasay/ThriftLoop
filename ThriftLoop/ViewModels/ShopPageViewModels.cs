using System.ComponentModel.DataAnnotations;

namespace ThriftLoop.ViewModels;

/// <summary>
/// Drives the public shop profile page (Views/Shop/Index.cshtml).
/// IsOwner controls whether inline editing controls are rendered.
/// </summary>
public class ShopPageViewModel
{
    public int ShopId { get; set; }
    public bool IsOwner { get; set; }

    public string ShopName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? BannerUrl { get; set; }
    public string? LogoUrl { get; set; }
    public string? StoreAddress { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    /// <summary>Listings belonging to this shop — loaded on Index.</summary>
    public IReadOnlyList<ThriftLoop.Models.Item> Items { get; set; } = new List<ThriftLoop.Models.Item>();

    /// <summary>Sold counts per item (ItemId -> Sold Count).</summary>
    public Dictionary<int, int> ItemSoldCounts { get; set; } = new();
}

public class SaveLocationDto
{
    public int ShopId { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? Address { get; set; }
}

/// <summary>
/// Payload for the SaveField AJAX endpoint (text fields only: ShopName, Bio).
/// Images are handled separately by SaveImage which accepts a multipart upload.
/// </summary>
public class SaveFieldDto
{
    [Required]
    public int ShopId { get; set; }

    /// <summary>One of: ShopName | Bio</summary>
    [Required]
    public string Field { get; set; } = string.Empty;

    /// <summary>New value. Null/empty is allowed for Bio.</summary>
    public string? Value { get; set; }
}