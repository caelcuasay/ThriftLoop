using System.ComponentModel.DataAnnotations;

namespace ThriftLoop.ViewModels;

/// <summary>
/// ViewModel for the Edit Item form.
/// Mirrors ItemCreateViewModel but also carries the item's primary key,
/// its current image URL (so the view can display it and the controller
/// can preserve it when no replacement is uploaded), and a RemoveImage flag.
/// </summary>
public class ItemEditViewModel
{
    // ── Identity ────────────────────────────────────────────────────────────

    /// <summary>Primary key — rendered as a hidden field and used to route the POST.</summary>
    public int Id { get; set; }

    // ── Core Fields ─────────────────────────────────────────────────────────

    [Required(ErrorMessage = "Title is required.")]
    [StringLength(100, MinimumLength = 3,
        ErrorMessage = "Title must be between 3 and 100 characters.")]
    [Display(Name = "Title")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Description is required.")]
    [StringLength(1000, MinimumLength = 10,
        ErrorMessage = "Description must be between 10 and 1 000 characters.")]
    [Display(Name = "Description")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Price is required.")]
    [Range(0.01, 99_999.99,
        ErrorMessage = "Price must be between ₱0.01 and ₱99,999.99.")]
    [DataType(DataType.Currency)]
    [Display(Name = "Price (₱)")]
    public decimal Price { get; set; }

    [Required(ErrorMessage = "Please select a category.")]
    [StringLength(50)]
    [Display(Name = "Category")]
    public string Category { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please select a condition.")]
    [StringLength(50)]
    [Display(Name = "Condition")]
    public string Condition { get; set; } = string.Empty;

    // ── Image ────────────────────────────────────────────────────────────────

    /// <summary>Optional replacement image uploaded by the user.</summary>
    [Display(Name = "Item Photo")]
    [DataType(DataType.Upload)]
    public IFormFile? Image { get; set; }

    /// <summary>
    /// The URL of the item's current image, populated from the database.
    /// Passed back through the form as a hidden field so the controller
    /// can keep it when no new image is uploaded.
    /// </summary>
    public string? ExistingImageUrl { get; set; }

    /// <summary>
    /// When true the controller will delete the existing image file and
    /// set ImageUrl to null, even if no replacement is uploaded.
    /// </summary>
    public bool RemoveImage { get; set; }

    // ── Select-List Options ──────────────────────────────────────────────────

    public static readonly IReadOnlyList<string> Categories = new[]
    {
        "Clothing",
        "Electronics",
        "Furniture",
        "Books",
        "Toys & Games",
        "Sports & Outdoors",
        "Home & Kitchen",
        "Collectibles",
        "Other"
    };

    public static readonly IReadOnlyList<string> Conditions = new[]
    {
        "New",
        "Like New",
        "Good",
        "Fair",
        "Poor"
    };
}