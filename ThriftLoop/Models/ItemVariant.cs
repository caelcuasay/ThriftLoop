namespace ThriftLoop.Models;

/// <summary>
/// A named variant grouping under an Item.
/// Examples: "Red", "Navy", "Classic Fit", "Default".
///
/// Every Item has at least one variant:
///   P2P items   → one "Default" variant, auto-created when the item is posted.
///   Shop items  → one or more seller-defined variants, each with their own SKUs.
///
/// The variant name is what the buyer sees when choosing between options on a
/// listing details page (e.g. a colour swatch row).
/// </summary>
public class ItemVariant
{
    public int Id { get; set; }

    /// <summary>FK → Items.Id</summary>
    public int ItemId { get; set; }

    /// <summary>
    /// Display name for this variant group.
    /// For P2P auto-generated variants this is always "Default".
    /// For shop variants this is whatever the seller names it (e.g. "Red", "Blue").
    /// Max 50 characters.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    // ── Navigation ────────────────────────────────────────────────────────────

    public Item? Item { get; set; }

    /// <summary>
    /// The SKUs under this variant — one per size/quantity combination.
    /// A P2P default variant always has exactly one SKU.
    /// A shop variant can have multiple SKUs (e.g. S, M, L, XL).
    /// </summary>
    public ICollection<ItemVariantSku> Skus { get; set; } = new List<ItemVariantSku>();
}