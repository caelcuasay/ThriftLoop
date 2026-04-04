// ViewModels/Admin/TransactionOversightViewModel.cs
using ThriftLoop.Models;
using ThriftLoop.Enums;

namespace ThriftLoop.ViewModels.Admin;

public class TransactionOversightViewModel
{
    // ── Transaction List ──────────────────────────────────────────────────────
    public IReadOnlyList<Transaction> Transactions { get; set; } = new List<Transaction>();

    // ── Filtering & Pagination ────────────────────────────────────────────────
    public string? SearchTerm { get; set; }
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }

    // ── Summary Stats ─────────────────────────────────────────────────────────
    public decimal TotalTransactionVolume { get; set; }
    public decimal TotalEscrowHolds { get; set; }
    public decimal TotalEscrowReleases { get; set; }
    public decimal TotalWithdrawals { get; set; }
    public decimal TotalTopUps { get; set; }
    public decimal TotalDeliveryFees { get; set; }

    // ── Computed Properties ───────────────────────────────────────────────────
    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;

    public string GetTransactionTypeIcon(TransactionType type)
    {
        return type switch
        {
            TransactionType.EscrowHold => "🔒",
            TransactionType.EscrowRelease => "🔓",
            TransactionType.Withdrawal => "📤",
            TransactionType.CashCollection => "💵",
            TransactionType.TopUp => "➕",
            TransactionType.DeliveryFeePayment => "🚚",
            _ => "📝"
        };
    }

    public string GetTransactionTypeClass(TransactionType type)
    {
        return type switch
        {
            TransactionType.EscrowHold => "tx-type--hold",
            TransactionType.EscrowRelease => "tx-type--release",
            TransactionType.Withdrawal => "tx-type--withdrawal",
            TransactionType.CashCollection => "tx-type--cash",
            TransactionType.TopUp => "tx-type--topup",
            TransactionType.DeliveryFeePayment => "tx-type--delivery",
            _ => "tx-type--default"
        };
    }

    public string GetTransactionStatusClass(TransactionStatus status)
    {
        return status switch
        {
            TransactionStatus.Completed => "status-completed",
            TransactionStatus.Pending => "status-pending",
            TransactionStatus.Failed => "status-failed",
            _ => "status-default"
        };
    }

    public string GetTransactionStatusText(TransactionStatus status)
    {
        return status switch
        {
            TransactionStatus.Completed => "Completed",
            TransactionStatus.Pending => "Pending",
            TransactionStatus.Failed => "Failed",
            _ => status.ToString()
        };
    }
}