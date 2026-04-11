namespace ThriftLoop.ViewModels;

/// <summary>
/// ViewModel for a single cart item during checkout.
/// </summary>
public class CartCheckoutItemViewModel
{
    public int CartItemId { get; set; }
    public int ItemId { get; set; }
    public string ItemTitle { get; set; } = string.Empty;
    public string? ItemImageUrl { get; set; }
    public int? ShopId { get; set; }
    public string? ShopName { get; set; }
    public string VariantName { get; set; } = string.Empty;
    public string? Size { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
    public int AvailableStock { get; set; }
}

/// <summary>
/// ViewModel for cart-based multi-item checkout.
/// </summary>
public class CartCheckoutViewModel
{
    public List<CartCheckoutItemViewModel> Items { get; set; } = new();
    public decimal BuyerBalance { get; set; }

    // Computed totals
    public decimal Subtotal { get; set; }
    public decimal DeliveryFeePerShop { get; set; } = 50m;
    public int ShopCount { get; set; }
    public decimal TotalDeliveryFee { get; set; }
    public decimal GrandTotal { get; set; }

    // Validation
    public bool HasSufficientBalance => BuyerBalance >= GrandTotal;
}
