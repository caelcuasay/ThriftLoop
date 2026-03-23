using ThriftLoop.Enums;

namespace ThriftLoop.Models;

// ── Domain model ──────────────────────────────────────────────────────────────

/// <summary>
/// Immutable audit record for every money movement in the system.
/// Create one per logical operation (escrow hold, release, top-up, etc.).
/// Never mutate a completed transaction — create a reversal instead.
/// </summary>
public class Transaction
{
    public int Id { get; set; }

    /// <summary>
    /// The order this transaction relates to. Null for top-ups and withdrawals
    /// that are not tied to a specific order.
    /// </summary>
    public int? OrderId { get; set; }

    /// <summary>The user whose balance is being debited (or the platform for top-ups).</summary>
    public int FromUserId { get; set; }

    /// <summary>The user whose balance is being credited.</summary>
    public int ToUserId { get; set; }

    /// <summary>Positive amount being transferred.</summary>
    public decimal Amount { get; set; }

    /// <summary>What kind of money movement this represents.</summary>
    public TransactionType Type { get; set; }

    /// <summary>Processing state of this transaction.</summary>
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;

    /// <summary>UTC timestamp when the transaction record was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp when the transaction reached a terminal state (Completed/Failed).</summary>
    public DateTime? CompletedAt { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────
    public Order? Order { get; set; }
    public User? FromUser { get; set; }
    public User? ToUser { get; set; }
}