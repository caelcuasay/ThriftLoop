using ThriftLoop.Enums;

namespace ThriftLoop.Models;

/// <summary>
/// A concrete, purchasable unit within a variant — defined by size, price, and quantity.
/// This is the entity that an Order directly references via ItemVariantSkuId.
///
/// For P2P items:
///   One SKU is auto-generated at item creation time.
///   Size and Price mirror Item.Size and Item.Price.
///   Quantity is always 1.
///   SkuStatus mirrors Item.Status (without StolenPendingCheckout — that lives on Item).
///
/// For shop items:
///   One SKU per size the seller offers under a given variant.
///   Price can differ per SKU to support variant-level pricing.
///   Quantity is set by the seller and decremented as orders come in.
///   SkuStatus drives availability independently per size.
///
/// Why Orders reference SkuId and not ItemId:
///   A buyer choosing "Red / Size M" at checkout is buying a specific SKU.
///   Locking price, size, and quantity at the SKU level means FinalPrice is
///   always consistent and inventory can be managed per size without ambiguity.
/// </summary>
public class ItemVariantSku
{
    public int Id { get; set; }

    /// <summary>FK → ItemVariants.Id</summary>
    public int VariantId { get; set; }

    /// <summary>
    /// Size label (e.g. "S", "M", "L", "XL", "Free Size", "One Size").
    /// Nullable — accessories or non-sized items may not have a size.
    /// For P2P items this mirrors Item.Size.
    /// </summary>
    public string? Size { get; set; }

    /// <summary>
    /// Price for this specific SKU.
    /// For P2P items this mirrors Item.Price and is locked at creation.
    /// For shop SKUs the seller can set different prices per size/variant.
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Number of units available for this SKU.
    /// Always 1 for P2P items.
    /// Set by the seller for shop items; decremented when orders are placed.
    /// </summary>
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Inventory state for this SKU.
    /// For P2P items, keep this in sync with Item.Status
    /// (Available ↔ Available, Reserved ↔ Reserved, Sold ↔ Sold).
    /// StolenPendingCheckout is intentionally not mirrored here — it lives on Item only.
    /// For shop SKUs this is updated independently as stock is depleted.
    /// </summary>
    public SkuStatus Status { get; set; } = SkuStatus.Available;

    // ── Navigation ────────────────────────────────────────────────────────────

    public ItemVariant? Variant { get; set; }

    /// <summary>All orders that reference this SKU.</summary>
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}