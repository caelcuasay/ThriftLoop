namespace ThriftLoop.Constants;

/// <summary>
/// Single source of truth for item listing options and stealable listing rules.
/// Used by ViewModels, Controllers, and any validation logic.
/// </summary>
public static class ItemConstants
{
    public static readonly IReadOnlyList<string> Categories =
    [
        "Tops", "Bottoms", "Dresses & Skirts", "Outerwear", "Footwear",
        "Accessories", "Vintage", "Activewear", "Bags", "Other"
    ];

    public static readonly IReadOnlyList<string> Conditions =
    [
        "New", "Like New", "Good", "Fair", "Poor"
    ];

    public static readonly IReadOnlyList<string> Sizes =
    [
        "XS", "S", "M", "L", "XL", "XXL", "XXXL"
    ];

    /// <summary>Allowed steal window durations in hours.</summary>
    public static readonly IReadOnlyList<int> StealDurations = [6, 12, 24];

    /// <summary>
    /// Flat premium added to the base price when a buyer steals a Stealable listing.
    /// Captured in Order.FinalPrice at confirmation time.
    /// </summary>
    public const decimal StealPremium = 50m;

    /// <summary>
    /// Grace period in hours after StealEndsAt during which the original getter
    /// can still finalise their purchase before the item reverts.
    /// </summary>
    public const int FinalizeWindowHours = 2;

    // ── Image upload limits ───────────────────────────────────────────────────

    /// <summary>Maximum number of images allowed per listing.</summary>
    public const int MaxImagesPerListing = 5;

    /// <summary>Maximum allowed file size per image in bytes (5 MB).</summary>
    public const long MaxImageSizeBytes = 5 * 1024 * 1024;
}