using ThriftLoop.Enums;

namespace ThriftLoop.Models;

// ── Domain model ──────────────────────────────────────────────────────────────

/// <summary>
/// Records a seller's request to cash out their available wallet balance.
/// Processing and completion are handled by an admin workflow (future feature).
/// </summary>
public class Withdrawal
{
    public int Id { get; set; }

    /// <summary>FK → Users.Id — the seller requesting the withdrawal.</summary>
    public int UserId { get; set; }

    /// <summary>Amount requested. Must not exceed the wallet's Balance at request time.</summary>
    public decimal Amount { get; set; }

    /// <summary>Preferred payout method.</summary>
    public WithdrawalMethod Method { get; set; }

    /// <summary>Current processing state.</summary>
    public WithdrawalStatus Status { get; set; } = WithdrawalStatus.Requested;

    /// <summary>UTC timestamp when the seller submitted the request.</summary>
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp when the withdrawal reached Completed status. Null until then.</summary>
    public DateTime? CompletedAt { get; set; }

    // ── Optional detail fields (populated at request time for processing) ─
    /// <summary>Bank account number or pickup reference — stored for admin use.</summary>
    public string? Reference { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────
    public User? User { get; set; }
}