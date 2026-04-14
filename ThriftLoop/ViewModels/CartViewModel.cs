using ThriftLoop.Models;
using ThriftLoop.Enums;

namespace ThriftLoop.ViewModels;

/// <summary>
/// ViewModel for displaying a single cart item in the cart page.
/// </summary>
public class CartItemViewModel
{
    public int CartItemId { get; set; }
    public int ItemId { get; set; }
    public int ItemVariantSkuId { get; set; }

    // Item details
    public string ItemTitle { get; set; } = string.Empty;
    public string? ItemImageUrl { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;

    // Shop info (null for P2P items, but cart only allows Shop items)
    public int? ShopId { get; set; }
    public string? ShopName { get; set; }

    // Seller info (for delivery fee grouping)
    public int SellerId { get; set; }

    // Variant and SKU details
    public string VariantName { get; set; } = string.Empty;
    public string? Size { get; set; }
    public decimal Price { get; set; }
    public int AvailableStock { get; set; }

    // Cart-specific
    public int Quantity { get; set; }
    public DateTime AddedAt { get; set; }

    /// <summary>
    /// True if the parent item has been disabled by the seller.
    /// Disabled items cannot be purchased.
    /// </summary>
    public bool IsItemDisabled { get; set; }

    /// <summary>
    /// True if the SKU has sufficient stock AND the item is not disabled.
    /// </summary>
    public bool IsValid => AvailableStock >= Quantity && !IsItemDisabled;

    // Computed
    public decimal LineTotal => Price * Quantity;

    /// <summary>
    /// Human-readable reason why this item cannot be purchased.
    /// </summary>
    public string? InvalidReason
    {
        get
        {
            if (IsItemDisabled)
                return "Item Unavailable";
            if (AvailableStock < Quantity)
                return $"Only {AvailableStock} available";
            return null;
        }
    }
}

/// <summary>
/// ViewModel for the cart index page.
/// </summary>
public class CartIndexViewModel
{
    public List<CartItemViewModel> Items { get; set; } = new();

    // Computed totals - based on selected items only
    public int TotalItems => Items.Where(i => i.IsValid).Sum(i => i.Quantity);
    public decimal Subtotal => Items.Where(i => i.IsValid).Sum(i => i.LineTotal);
    public decimal DeliveryFeePerShop { get; set; } = 50m; // From ItemConstants.DeliveryFee

    /// <summary>
    /// Returns the number of unique shops among valid items.
    /// Used for calculating total delivery fee.
    /// </summary>
    public int UniqueShopCount => Items
        .Where(i => i.IsValid)
        .Select(i => i.ShopId)
        .Distinct()
        .Count();

    public decimal TotalDeliveryFee => DeliveryFeePerShop * UniqueShopCount;
    public decimal GrandTotal => Subtotal + TotalDeliveryFee;

    // Validation
    public bool HasValidItems => Items.All(i => i.IsValid);
    public bool IsEmpty => Items.Count == 0;

    /// <summary>
    /// Count of items that are disabled or out of stock.
    /// </summary>
    public int InvalidItemCount => Items.Count(i => !i.IsValid);
}

/// <summary>
/// DTO for adding an item to cart via AJAX.
/// </summary>
public class AddToCartDto
{
    public int ItemId { get; set; }
    public int SkuId { get; set; }
    public int Quantity { get; set; } = 1;
}

/// <summary>
/// DTO for updating cart item quantity via AJAX.
/// </summary>
public class UpdateCartQuantityDto
{
    public int CartItemId { get; set; }
    public int Quantity { get; set; }
}

/// <summary>
/// Response DTO for cart operations (update quantity, remove, etc.)
/// </summary>
public class CartOperationResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int Quantity { get; set; }
    public string LineTotal { get; set; } = string.Empty;
    public string Subtotal { get; set; } = string.Empty;
    public string TotalDeliveryFee { get; set; } = string.Empty;
    public string GrandTotal { get; set; } = string.Empty;
    public int CartCount { get; set; }
    public bool IsEmpty { get; set; }
    public int UniqueShopCount { get; set; }
}