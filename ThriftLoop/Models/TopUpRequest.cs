using ThriftLoop.Enums;

namespace ThriftLoop.Models;

/// <summary>
/// Records a user's GCash top-up request via screenshot upload.
/// Auto-approved on valid OCR match, or queued for manual review.
/// </summary>
public class TopUpRequest
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public decimal Amount { get; set; }

    public string ReferenceNumber { get; set; } = string.Empty;

    public TopUpStatus Status { get; set; } = TopUpStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ProcessedAt { get; set; }

    public bool IsAutoApproved { get; set; } = false;

    /// <summary>True if this auto-approved transaction needs admin review.</summary>
    public bool NeedsAdminReview { get; set; } = false;

    public float? OcrConfidence { get; set; }

    public string? ScreenshotPath { get; set; }

    public string? RejectionReason { get; set; }

    public string? VoidReason { get; set; }

    public int? ProcessedBy { get; set; }

    public bool? AccountNumberMatched { get; set; }

    // Navigation
    public User? User { get; set; }
}