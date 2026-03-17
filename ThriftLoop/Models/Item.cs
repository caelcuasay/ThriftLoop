namespace ThriftLoop.Models;

/// <summary>
/// Domain model — mirrors the Items database table.
/// Keep this class free of UI concerns (no [Required], no display annotations).
/// All validation annotations belong in ItemCreateViewModel.
/// </summary>
public class Item
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public string Category { get; set; } = string.Empty;

    public string Condition { get; set; } = string.Empty;

    /// <summary>
    /// Standard clothing size (XS / S / M / L / XL / XXL / XXXL).
    /// Nullable — not all item types carry a standard size (e.g. bags, accessories).
    /// </summary>
    public string? Size { get; set; }

    /// <summary>Relative or absolute URL to the uploaded image.</summary>
    public string? ImageUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Relationships ──────────────────────────────────────────────────────

    /// <summary>FK → Users.Id</summary>
    public int UserId { get; set; }

    /// <summary>Navigation property — not loaded by default (use .Include() when needed).</summary>
    public User? User { get; set; }
}