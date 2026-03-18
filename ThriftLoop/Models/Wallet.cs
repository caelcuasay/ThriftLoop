namespace ThriftLoop.Models;

/// <summary>
/// One wallet per user. Holds available (withdrawable) balance and
/// funds currently locked in escrow for pending orders.
/// </summary>
public class Wallet
{
    public int Id { get; set; }

    /// <summary>FK → Users.Id</summary>
    public int UserId { get; set; }

    /// <summary>
    /// Freely available funds the user can spend or withdraw.
    /// For demo purposes, new wallets are seeded with ₱1,000.
    /// </summary>
    public decimal Balance { get; set; } = 0m;

    /// <summary>
    /// Funds held in escrow for pending orders.
    /// Money moves here from Balance on ConfirmOrder and returns
    /// to the seller's Balance when the buyer marks delivery.
    /// </summary>
    public decimal PendingBalance { get; set; } = 0m;

    /// <summary>UTC timestamp of the last balance mutation.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation ────────────────────────────────────────────────────────
    public User? User { get; set; }
}