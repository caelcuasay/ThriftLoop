using ThriftLoop.Models;

namespace ThriftLoop.Services.WalletManagement.Interface;

public interface IWalletService
{
    // ── Wallet access ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns the wallet for <paramref name="userId"/>. If one does not exist yet,
    /// creates and persists it with a ₱1,000 demo seed balance.
    /// This is the preferred method for all consumer code — never returns null.
    /// </summary>
    Task<Wallet> GetOrCreateWalletAsync(int userId);

    // ── Escrow (Wallet payment path) ───────────────────────────────────────

    /// <summary>
    /// Holds <paramref name="amount"/> in escrow for the buyer when a Wallet order
    /// is confirmed.
    ///
    /// Steps:
    ///   buyer.Balance        -= amount
    ///   buyer.PendingBalance += amount
    ///   Writes an EscrowHold Transaction (Completed).
    ///
    /// Returns <c>true</c> on success, <c>false</c> when the buyer has insufficient
    /// available balance (PendingBalance is unchanged on failure).
    /// </summary>
    Task<bool> HoldEscrowAsync(int orderId, int buyerId, decimal amount);

    /// <summary>
    /// Releases escrowed funds to the seller when the buyer marks delivery.
    ///
    /// Steps:
    ///   buyer.PendingBalance -= amount
    ///   seller.Balance       += amount
    ///   Writes an EscrowRelease Transaction (Completed).
    /// </summary>
    Task ReleaseEscrowAsync(int orderId, int buyerId, int sellerId, decimal amount);

    // ── Cash on Delivery ───────────────────────────────────────────────────

    /// <summary>
    /// Credits the seller's wallet when a COD order's cash is collected.
    ///
    /// Steps:
    ///   seller.Balance += amount
    ///   Writes a CashCollection Transaction (Completed).
    ///
    /// The fromUserId is the buyer's ID for audit purposes.
    /// </summary>
    Task RecordCashCollectionAsync(int orderId, int buyerId, int sellerId, decimal amount);

    // ── Top-up ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds funds to a user's available balance (demo top-up / future gateway).
    /// Writes a TopUp Transaction (Completed).
    /// </summary>
    Task TopUpAsync(int userId, decimal amount);

    // ── Withdrawal ─────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a pending Withdrawal request and deducts the amount from the
    /// user's available balance immediately (funds are reserved for payout).
    ///
    /// Returns <c>false</c> if the user has insufficient balance.
    /// </summary>
    Task<bool> RequestWithdrawalAsync(int userId, decimal amount, WithdrawalMethod method, string? reference);
}