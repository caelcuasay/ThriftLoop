// ViewModels/Admin/AdminDashboardViewModel.cs
using ThriftLoop.Models;
using ThriftLoop.Enums;

namespace ThriftLoop.ViewModels.Admin;

public class AdminDashboardViewModel
{
    // ── Statistics Cards ──────────────────────────────────────────────────────
    public int TotalUsers { get; set; }
    public int TotalSellers { get; set; }
    public int TotalRiders { get; set; }
    public int TotalOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal PendingWithdrawals { get; set; }
    public int PendingSellerApprovals { get; set; }
    public int PendingRiderApprovals { get; set; }

    // ── Recent Activity Lists ─────────────────────────────────────────────────
    public IReadOnlyList<User> RecentUsers { get; set; } = new List<User>();
    public IReadOnlyList<Withdrawal> RecentWithdrawals { get; set; } = new List<Withdrawal>();
    public IReadOnlyList<SellerProfile> RecentSellerApplications { get; set; } = new List<SellerProfile>();
    public IReadOnlyList<Rider> RecentRiderApplications { get; set; } = new List<Rider>();

    // ── Computed Properties ───────────────────────────────────────────────────
    public decimal TotalPlatformValue => TotalRevenue + PendingWithdrawals;
}