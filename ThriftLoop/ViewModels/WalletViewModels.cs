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