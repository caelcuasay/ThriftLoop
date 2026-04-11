using ThriftLoop.Models;

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

    // Variant and SKU details
    public string VariantName { get; set; } = string.Empty;
    public string? Size { get; set; }
    public decimal Price { get; set; }
    public int AvailableStock { get; set; }

    // Cart-specific
    public int Quantity { get; set; }
    public DateTime AddedAt { get; set; }

    // Computed
    public decimal LineTotal => Price * Quantity;
    public bool IsValid => AvailableStock >= Quantity;
}

/// <summary>
/// ViewModel for the cart index page.
/// </summary>
public class CartIndexViewModel
{
    public List<CartItemViewModel> Items { get; set; } = new();

    // Computed totals
    public int TotalItems => Items.Sum(i => i.Quantity);
    public decimal Subtotal => Items.Sum(i => i.LineTotal);
    public decimal DeliveryFeePerItem { get; set; } = 50m; // From ItemConstants.DeliveryFee
    public decimal TotalDeliveryFee => DeliveryFeePerItem * Items.Count;
    public decimal GrandTotal => Subtotal + TotalDeliveryFee;

    // Validation
    public bool HasValidItems => Items.All(i => i.IsValid);
    public bool IsEmpty => Items.Count == 0;
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
