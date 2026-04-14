using System.ComponentModel.DataAnnotations;

namespace ThriftLoop.ViewModels;

/// <summary>
/// DTO for applying a discount to an item.
/// </summary>
public class ApplyDiscountDto
{
    [Required]
    public int ItemId { get; set; }

    [Required]
    [Range(1, 99, ErrorMessage = "Discount percentage must be between 1% and 99%.")]
    public decimal DiscountPercentage { get; set; }

    /// <summary>
    /// Optional expiration date for the discount.
    /// Null means indefinite (no expiration).
    /// </summary>
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>
/// Response DTO for discount operations.
/// </summary>
public class DiscountResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public decimal NewPrice { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal SavingsAmount { get; set; }
    public string? ExpiresAt { get; set; }
    public bool IsIndefinite { get; set; }
}

/// <summary>
/// ViewModel for displaying discounted item info.
/// </summary>
public class DiscountedItemViewModel
{
    public int ItemId { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal SavingsAmount { get; set; }
    public DateTime? DiscountExpiresAt { get; set; }
    public bool HasActiveDiscount { get; set; }
    public bool IsIndefinite => !DiscountExpiresAt.HasValue;

    public string FormattedCurrentPrice => $"₱{CurrentPrice:N2}";
    public string FormattedOriginalPrice => $"₱{OriginalPrice:N2}";
    public string FormattedSavings => $"₱{SavingsAmount:N2}";
    public string DiscountBadgeText => $"-{DiscountPercentage:F0}%";

    public string? ExpiryText
    {
        get
        {
            if (!DiscountExpiresAt.HasValue) return null;

            var timeLeft = DiscountExpiresAt.Value - DateTime.UtcNow;
            if (timeLeft.TotalDays >= 1)
                return $"Ends in {(int)timeLeft.TotalDays} day{((int)timeLeft.TotalDays != 1 ? "s" : "")}";
            if (timeLeft.TotalHours >= 1)
                return $"Ends in {(int)timeLeft.TotalHours} hour{((int)timeLeft.TotalHours != 1 ? "s" : "")}";
            if (timeLeft.TotalMinutes > 0)
                return $"Ends in {(int)timeLeft.TotalMinutes} min";
            return "Ending soon";
        }
    }
}