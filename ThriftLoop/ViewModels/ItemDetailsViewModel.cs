using ThriftLoop.Models;
using ThriftLoop.Enums;

namespace ThriftLoop.ViewModels;

/// <summary>
/// ViewModel for the Item Details page that supports variant selection.
/// Handles both P2P items (single variant) and Shop items (multiple variants).
/// </summary>
public class ItemDetailsViewModel
{
    // ── Item Core Info ─────────────────────────────────────────────────────────
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public List<string> ImageUrls { get; set; } = new();
    public DateTime CreatedAt { get; set; }

    // ── Owner Info ────────────────────────────────────────────────────────────
    public int UserId { get; set; }
    public string? SellerName { get; set; }
    public string? SellerEmail { get; set; }

    // ── Shop Info (for shop items) ────────────────────────────────────────────
    public int? ShopId { get; set; }
    public string? ShopName { get; set; }
    public string? ShopLogoUrl { get; set; }
    public bool IsShopItem => ShopId.HasValue;

    // ── Stealable Listing Info ────────────────────────────────────────────────
    public ListingType ListingType { get; set; }
    public ItemStatus Status { get; set; }
    public int? StealDurationHours { get; set; }
    public DateTime? StealEndsAt { get; set; }
    public int? CurrentWinnerId { get; set; }
    public int? OriginalGetterUserId { get; set; }

    // ── Computed Properties for Stealable UI ──────────────────────────────────
    public bool IsStealable => ListingType == ListingType.Stealable;
    public bool IsAvailable => Status == ItemStatus.Available;
    public bool IsReserved => Status == ItemStatus.Reserved;
    public bool IsStolenPendingCheckout => Status == ItemStatus.StolenPendingCheckout;
    public bool IsSold => Status == ItemStatus.Sold;

    public bool IsInFinalizeWindow =>
        IsStealable &&
        Status == ItemStatus.Reserved &&
        StealEndsAt.HasValue &&
        DateTime.UtcNow > StealEndsAt.Value &&
        DateTime.UtcNow <= StealEndsAt.Value.AddHours(2);

    public DateTime? FinalizeDeadline =>
        StealEndsAt.HasValue ? StealEndsAt.Value.AddHours(2) : null;

    // ── User Context ──────────────────────────────────────────────────────────
    public int? CurrentUserId { get; set; }
    public bool IsOwner => CurrentUserId.HasValue && CurrentUserId.Value == UserId;
    public bool IsCurrentWinner => CurrentUserId.HasValue && CurrentWinnerId == CurrentUserId.Value;
    public bool IsOriginalGetter => CurrentUserId.HasValue && OriginalGetterUserId == CurrentUserId.Value;
    public bool IsRider { get; set; }
    public bool HasCompleteProfile { get; set; }

    // ── Variant Selection ─────────────────────────────────────────────────────
    public List<ItemVariantDisplayModel> Variants { get; set; } = new();

    /// <summary>
    /// The minimum price across all available SKUs.
    /// Used for "From ₱X" display on the details page header.
    /// </summary>
    public decimal StartingPrice { get; set; }

    /// <summary>
    /// For non-stealable P2P items with a single SKU, this is the price.
    /// For stealable items, this is the base price before premium.
    /// </summary>
    public decimal BasePrice { get; set; }
}

/// <summary>
/// Represents a variant group (e.g., "Red", "Blue") for display on the details page.
/// </summary>
public class ItemVariantDisplayModel
{
    public int VariantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<ItemSkuDisplayModel> Skus { get; set; } = new();

    // For UI grouping
    public bool HasMultipleSkus => Skus.Count > 1;
}

/// <summary>
/// Represents a purchasable SKU (size + price + quantity) for display.
/// </summary>
public class ItemSkuDisplayModel
{
    public int SkuId { get; set; }
    public string? Size { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public SkuStatus Status { get; set; }

    public bool IsAvailable => Status == SkuStatus.Available && Quantity > 0;
    public string DisplayName => string.IsNullOrEmpty(Size) ? "Standard" : $"Size {Size}";
    public string PriceFormatted => $"₱{Price:N2}";
}

/// <summary>
/// DTO for the AJAX request when a user selects a variant/SKU.
/// </summary>
public class SelectedSkuDto
{
    public int ItemId { get; set; }
    public int SkuId { get; set; }
}

/// <summary>
/// Response DTO for SKU selection (price and availability).
/// </summary>
public class SkuSelectionResponse
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int SkuId { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public bool IsAvailable { get; set; }
    public string? Size { get; set; }
}