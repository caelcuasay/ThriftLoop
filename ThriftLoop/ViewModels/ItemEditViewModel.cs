using System.ComponentModel.DataAnnotations;

namespace ThriftLoop.ViewModels;

public class ItemEditViewModel
{
    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>Primary key — rendered as a hidden field, used to route the POST.</summary>
    public int Id { get; set; }

    // ── Core Fields ───────────────────────────────────────────────────────────

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
    [Range(0.01, 99_999.99, ErrorMessage = "Price must be between ₱0.01 and ₱99,999.99.")]
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

    [StringLength(10)]
    [Display(Name = "Size (optional)")]
    public string? Size { get; set; }

    // ── Images ────────────────────────────────────────────────────────────────

    /// <summary>New images added by the user on the Edit form (name="NewImages").</summary>
    [Display(Name = "Add Photos")]
    [DataType(DataType.Upload)]
    public List<IFormFile>? NewImages { get; set; }

    /// <summary>
    /// The item's current image URLs from the database.
    /// Round-tripped through the form as repeated hidden inputs (name="ExistingImageUrls").
    /// The controller reads this to know which existing images to keep.
    /// </summary>
    public List<string> ExistingImageUrls { get; set; } = new();

    /// <summary>
    /// URLs the user removed in the UI.
    /// Injected by JS as hidden inputs (name="RemovedImageUrls") when a thumbnail × is clicked.
    /// The controller deletes these files from disk and removes them from the DB list.
    /// </summary>
    public List<string> RemovedImageUrls { get; set; } = new();

    // ── Select-List Options ───────────────────────────────────────────────────

    public static readonly IReadOnlyList<string> Categories = new[]
    {
        "Tops", "Bottoms", "Dresses & Skirts", "Outerwear", "Footwear",
        "Accessories", "Vintage", "Activewear", "Bags", "Other"
    };

    public static readonly IReadOnlyList<string> Conditions = new[]
    {
        "New", "Like New", "Good", "Fair", "Poor"
    };

    public static readonly IReadOnlyList<string> Sizes = new[]
    {
        "XS", "S", "M", "L", "XL", "XXL", "XXXL"
    };
}