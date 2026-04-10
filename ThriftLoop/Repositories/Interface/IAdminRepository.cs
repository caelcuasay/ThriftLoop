// Repositories/Interface/IAdminRepository.cs
using ThriftLoop.Enums;
using ThriftLoop.Models;

namespace ThriftLoop.Repositories.Interface;

public interface IAdminRepository
{
    // ════════════════════════════════════════════════════════════════════════════
    //  DASHBOARD STATS
    // ════════════════════════════════════════════════════════════════════════════

    Task<DashboardStats> GetDashboardStatsAsync();
    Task<IReadOnlyList<User>> GetRecentUsersAsync(int count);
    Task<IReadOnlyList<Withdrawal>> GetPendingWithdrawalsAsync(int count);
    Task<IReadOnlyList<SellerProfile>> GetPendingSellerApplicationsAsync(int count);
    Task<IReadOnlyList<Rider>> GetPendingRiderApplicationsAsync(int count);

    // ════════════════════════════════════════════════════════════════════════════
    //  USER MANAGEMENT
    // ════════════════════════════════════════════════════════════════════════════

    Task<(IReadOnlyList<User> Users, int TotalCount)> GetUsersAsync(
        string? searchTerm,
        string? roleFilter,
        int page,
        int pageSize);

    Task<bool> UpdateUserRoleAsync(int userId, UserRole newRole);
    Task<bool> ToggleUserStatusAsync(int userId, bool isDisabled);

    // ════════════════════════════════════════════════════════════════════════════
    //  SELLER APPROVALS
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gets seller applications, optionally filtered by status string
    /// ("Pending", "Approved", "Rejected").
    /// </summary>
    Task<IReadOnlyList<SellerProfile>> GetSellerApplicationsAsync(string? statusFilter);

    /// <summary>
    /// Returns a single seller application with its User navigated in,
    /// so the admin can inspect all submitted details (StoreAddress, GovIdUrl, etc.).
    /// Returns null when not found.
    /// </summary>
    Task<SellerProfile?> GetSellerApplicationByIdAsync(int applicationId);

    /// <summary>
    /// Approves the application: sets status to Approved and elevates User.Role to Seller.
    /// </summary>
    Task<bool> ApproveSellerAsync(int applicationId);

    /// <summary>
    /// Rejects the application: sets status to Rejected. User.Role is unchanged.
    /// </summary>
    Task<bool> RejectSellerAsync(int applicationId);

    // ════════════════════════════════════════════════════════════════════════════
    //  RIDER APPROVALS
    // ════════════════════════════════════════════════════════════════════════════

    Task<IReadOnlyList<Rider>> GetRiderApplicationsAsync(string? statusFilter);

    /// <summary>
    /// Returns a single rider application with full details for admin review.
    /// Returns null when not found.
    /// </summary>
    Task<Rider?> GetRiderApplicationByIdAsync(int riderId);

    Task<bool> ApproveRiderAsync(int riderId);
    Task<bool> RejectRiderAsync(int riderId);

    // ════════════════════════════════════════════════════════════════════════════
    //  WITHDRAWAL MANAGEMENT
    // ════════════════════════════════════════════════════════════════════════════

    Task<IReadOnlyList<Withdrawal>> GetWithdrawalsAsync(string? statusFilter);
    Task<bool> MarkWithdrawalProcessedAsync(int withdrawalId);
    Task<bool> MarkWithdrawalCompletedAsync(int withdrawalId);

    // ════════════════════════════════════════════════════════════════════════════
    //  TRANSACTION OVERSIGHT
    // ════════════════════════════════════════════════════════════════════════════

    Task<(IReadOnlyList<Transaction> Transactions, int TotalCount)> GetAllTransactionsAsync(
        string? searchTerm,
        int page,
        int pageSize);

    // ════════════════════════════════════════════════════════════════════════════
    //  SYSTEM INFO
    // ════════════════════════════════════════════════════════════════════════════

    Task<SystemInfo> GetSystemInfoAsync();

    // Repositories/Interface/IAdminRepository.cs - Add method
    // Add this to the interface:

    // RIDER APPROVALS section - add:
    Task<bool> RejectRiderWithReasonAsync(int riderId, string reason);
}

// ════════════════════════════════════════════════════════════════════════════════
//  DTOs / Result Classes
// ════════════════════════════════════════════════════════════════════════════════

public class DashboardStats
{
    public int TotalUsers { get; set; }
    public int TotalSellers { get; set; }
    public int TotalRiders { get; set; }
    public int TotalOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal PendingWithdrawals { get; set; }
    public int PendingSellerApprovals { get; set; }
    public int PendingRiderApprovals { get; set; }
}

public class SystemInfo
{
    public int TotalUsers { get; set; }
    public int TotalSellers { get; set; }
    public int TotalRiders { get; set; }
    public int TotalItems { get; set; }
    public int TotalOrders { get; set; }
    public int TotalDeliveries { get; set; }
    public int CompletedDeliveries { get; set; }
    public DateTime? EarliestUserCreatedAt { get; set; }
    public DateTime? LatestOrderDate { get; set; }
    public decimal TotalEscrowHeld { get; set; }
    public decimal TotalPlatformBalance { get; set; }
}