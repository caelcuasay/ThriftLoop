using ThriftLoop.Enums;
using ThriftLoop.Models;
using ThriftLoop.Constants;

namespace ThriftLoop.ViewModels;

/// <summary>
/// Carries everything the Wallet dashboard view needs: balances,
/// recent transactions, and pending withdrawal requests.
/// </summary>
public class WalletIndexViewModel
{
    // ── Balances ──────────────────────────────────────────────────────────
    public decimal Balance { get; init; }
    public decimal PendingBalance { get; init; }
    public decimal TotalBalance => Balance + PendingBalance;

    // ── History ───────────────────────────────────────────────────────────
    public IReadOnlyList<Transaction> RecentTransactions { get; init; } = [];
    public IReadOnlyList<Withdrawal> Withdrawals { get; init; } = [];

    // ── Context ───────────────────────────────────────────────────────────
    public int CurrentUserId { get; init; }
}

/// <summary>
/// Posted from the Withdraw form.
/// </summary>
public class WithdrawViewModel
{
    /// <summary>
    /// Must be at least <see cref="WalletConstants.MinWithdrawalAmount"/>.
    /// Validated in the controller against the user's live wallet balance.
    /// </summary>
    public decimal Amount { get; set; }

    public WithdrawalMethod Method { get; set; } = WithdrawalMethod.BankTransfer;

    /// <summary>Bank account number or pickup reference.</summary>
    public string? Reference { get; set; }
}

/// <summary>
/// View model for the Add Funds (GCash top-up) page.
/// </summary>
public class AddFundsViewModel
{
    /// <summary>
    /// True if the admin has uploaded a QR code.
    /// </summary>
    public bool HasQRCode => !string.IsNullOrEmpty(QRCodePath);

    /// <summary>
    /// Path to the GCash QR code image (relative to wwwroot).
    /// </summary>
    public string? QRCodePath { get; set; }

    /// <summary>
    /// Amount the user wants to top up (posted from form).
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Reference number from GCash receipt (posted from form).
    /// Optional - OCR will extract if not provided.
    /// </summary>
    public string? ReferenceNumber { get; set; }
}