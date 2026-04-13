namespace ThriftLoop.Models;

/// <summary>
/// Represents a line item within an order.
/// This allows a single order to contain multiple SKUs (e.g., bulk purchases from a shop).
/// For backward compatibility, Order.ItemId and Order.ItemVariantSkuId still exist
/// for single-item orders (P2P or single-SKU shop purchases).
/// </summary>
public class OrderItem
{
    public int Id { get; set; }

    /// <summary>FK → Orders.Id</summary>
    public int OrderId { get; set; }

    /// <summary>FK → ItemVariantSkus.Id — the specific SKU purchased.</summary>
    public int ItemVariantSkuId { get; set; }

    /// <summary>Quantity of this SKU in the order.</summary>
    public int Quantity { get; set; }

    /// <summary>Unit price at the time of purchase (snapshot).</summary>
    public decimal UnitPrice { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────────
    public Order? Order { get; set; }
    public ItemVariantSku? ItemVariantSku { get; set; }
}