using System.ComponentModel.DataAnnotations;
using ThriftLoop.Constants;

namespace ThriftLoop.ViewModels;

public class ItemCreateViewModel : IValidatableObject
{
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

    /// <summary>
    /// Up to <see cref="ItemConstants.MaxImagesPerListing"/> images uploaded from the Create form.
    /// Bound from a multiple file input with name="Images".
    /// </summary>
    [Display(Name = "Item Photos (optional)")]
    [DataType(DataType.Upload)]
    public List<IFormFile>? Images { get; set; }

    // ── Stealable Listing Fields ──────────────────────────────────────────────

    [Display(Name = "Make this a Stealable listing")]
    public bool IsStealable { get; set; }

    [Display(Name = "Steal Window")]
    public int? StealDurationHours { get; set; }

    // ── IValidatableObject ────────────────────────────────────────────────────

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (IsStealable)
        {
            if (StealDurationHours is null)
            {
                yield return new ValidationResult(
                    "Please select a Steal window duration.",
                    [nameof(StealDurationHours)]);
            }
            else if (!ItemConstants.StealDurations.Contains(StealDurationHours.Value))
            {
                yield return new ValidationResult(
                    $"Steal window must be {string.Join(", ", ItemConstants.StealDurations)} hours.",
                    [nameof(StealDurationHours)]);
            }
        }
    }

    // ── Select-List Options (delegates to constants) ──────────────────────────

    public static IReadOnlyList<string> Categories => ItemConstants.Categories;
    public static IReadOnlyList<string> Conditions => ItemConstants.Conditions;
    public static IReadOnlyList<string> Sizes => ItemConstants.Sizes;
    public static IReadOnlyList<int> StealDurations => ItemConstants.StealDurations;
}