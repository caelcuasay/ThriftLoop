using System.ComponentModel.DataAnnotations;

namespace ThriftLoop.ViewModels;

/// <summary>
/// ViewModel for the Create Item form.
/// All user-facing validation annotations live here, keeping the domain
/// model (Item.cs) focused purely on the database schema.
/// </summary>
public class ItemCreateViewModel : IValidatableObject
{
    // ── Core Fields ────────────────────────────────────────────────────────

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

    /// <summary>
    /// Optional clothing size. Not required — some categories (bags, accessories)
    /// do not have a standard size.
    /// </summary>
    [StringLength(10)]
    [Display(Name = "Size (optional)")]
    public string? Size { get; set; }

    [Display(Name = "Item Photo (optional)")]
    [DataType(DataType.Upload)]
    public IFormFile? Image { get; set; }
    public string? ImageUrl { get; set; }

    // ── Stealable Listing Fields ───────────────────────────────────────────

    /// <summary>
    /// When true the listing is created as a Stealable listing.
    /// Bound to a toggle/checkbox in the form.
    /// </summary>
    [Display(Name = "Make this a Stealable listing")]
    public bool IsStealable { get; set; }

    /// <summary>
    /// The seller-chosen Steal window duration in hours (6, 12, or 24).
    /// Required only when <see cref="IsStealable"/> is true.
    /// Validated via <see cref="Validate"/>.
    /// </summary>
    [Display(Name = "Steal Window")]
    public int? StealDurationHours { get; set; }

    // ── IValidatableObject ─────────────────────────────────────────────────

    /// <summary>
    /// Cross-field validation: StealDurationHours is required and must be one
    /// of the allowed values whenever the listing is marked as Stealable.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (IsStealable)
        {
            if (StealDurationHours is null)
            {
                yield return new ValidationResult(
                    "Please select a Steal window duration.",
                    new[] { nameof(StealDurationHours) });
            }
            else if (!StealDurations.Contains(StealDurationHours.Value))
            {
                yield return new ValidationResult(
                    "Steal window must be 6, 12, or 24 hours.",
                    new[] { nameof(StealDurationHours) });
            }
        }
    }

    // ── Select-List Options ────────────────────────────────────────────────

    public static readonly IReadOnlyList<string> Categories = new[]
    {
        "Tops",
        "Bottoms",
        "Dresses & Skirts",
        "Outerwear",
        "Footwear",
        "Accessories",
        "Vintage",
        "Activewear",
        "Bags",
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

    public static readonly IReadOnlyList<string> Sizes = new[]
    {
        "XS",
        "S",
        "M",
        "L",
        "XL",
        "XXL",
        "XXXL"
    };

    /// <summary>The three allowed Steal window durations displayed in the form dropdown.</summary>
    public static readonly IReadOnlyList<int> StealDurations = new[] { 6, 12, 24 };
}