namespace ThriftLoop.Enums;

public enum ListingType { Standard, Stealable }

public enum ItemStatus
{
    Available,
    Reserved,
    Sold,
    StolenPendingCheckout,
    /// <summary>
    /// Listing is temporarily hidden from public views.
    /// Seller can toggle this on/off at any time, even if items are in carts.
    /// </summary>
    Disabled
}

/// <summary>
/// Inventory state of a single ItemVariantSku row.
///
/// This is intentionally separate from ItemStatus. ItemStatus drives the
/// P2P steal lifecycle (Reserved, StolenPendingCheckout, etc.) and lives
/// on the Item row. SkuStatus only tracks whether a given size/variant
/// combination can still be purchased — it applies to both P2P default
/// SKUs and Seller shop SKUs uniformly.
/// </summary>
public enum SkuStatus
{
    /// <summary>Stock is available and the SKU can be added to an order.</summary>
    Available,

    /// <summary>
    /// Reserved by a pending order. For P2P SKUs this mirrors Item.Status.
    /// For shop SKUs this means the quantity has been decremented.
    /// </summary>
    Reserved,

    /// <summary>
    /// Fully sold out — Quantity has reached zero or the order was completed.
    /// </summary>
    Sold
}