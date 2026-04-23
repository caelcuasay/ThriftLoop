// ViewModels/Admin/TopUpReviewViewModel.cs
using ThriftLoop.Enums;
using ThriftLoop.Models;

namespace ThriftLoop.ViewModels.Admin;

/// <summary>
/// View model for the admin top-up request review page.
/// </summary>
public class TopUpReviewViewModel
{
    /// <summary>
    /// List of top-up requests to display.
    /// </summary>
    public IReadOnlyList<TopUpRequest> Requests { get; init; } = [];

    /// <summary>
    /// Current filter applied (null = all active requests needing attention).
    /// </summary>
    public TopUpStatus? CurrentFilter { get; init; }

    /// <summary>
    /// Current page number.
    /// </summary>
    public int CurrentPage { get; init; } = 1;

    /// <summary>
    /// Total number of pages available.
    /// </summary>
    public int TotalPages { get; init; }

    /// <summary>
    /// Total number of requests matching the current filter.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Number of requests pending OCR/processing.
    /// </summary>
    public int PendingCount { get; init; }

    /// <summary>
    /// Number of requests needing manual review.
    /// </summary>
    public int NeedsReviewCount { get; init; }

    /// <summary>
    /// Number of auto-approved requests still pending admin review.
    /// </summary>
    public int AutoApprovedPendingReviewCount { get; init; }

    /// <summary>
    /// Number of high-value (>₱300) requests needing attention.
    /// </summary>
    public int HighValuePendingCount { get; init; }

    /// <summary>
    /// Priority threshold for high-value transactions.
    /// </summary>
    public decimal PriorityThreshold => 300m;

    /// <summary>
    /// Whether there is a previous page available.
    /// </summary>
    public bool HasPreviousPage => CurrentPage > 1;

    /// <summary>
    /// Whether there is a next page available.
    /// </summary>
    public bool HasNextPage => CurrentPage < TotalPages;
}