namespace ThriftLoop.Enums;

/// <summary>
/// Processing status of a GCash top-up request.
/// </summary>
public enum TopUpStatus
{
    /// <summary>
    /// Request created, awaiting processing (OCR or manual review).
    /// </summary>
    Pending,

    /// <summary>
    /// Successfully processed and credited to wallet.
    /// </summary>
    Approved,

    /// <summary>
    /// Rejected due to duplicate reference, fraud detection, or invalid receipt.
    /// </summary>
    Rejected,

    /// <summary>
    /// Low OCR confidence - requires manual admin review.
    /// </summary>
    NeedsReview,

    /// <summary>
    /// Voided - Transaction deemed invalid/illegible by admin.
    /// Funds were never credited.
    /// </summary>
    Voided
}