// Repositories/Interface/IAdminRepository.cs
using ThriftLoop.Enums;
using ThriftLoop.Models;

namespace ThriftLoop.Repositories.Interface;

public interface IAdminRepository
{
    // ════════════════════════════════════════════════════════════════════════════
    //  DASHBOARD STATS
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns aggregated platform statistics for the admin dashboard.
    /// </summary>
    Task<DashboardStats> GetDashboardStatsAsync();

    /// <summary>
    /// Gets the most recently registered users.
    /// </summary>
    Task<IReadOnlyList<User>> GetRecentUsersAsync(int count);

    /// <summary>
    /// Gets pending withdrawal requests.
    /// </summary>
    Task<IReadOnlyList<Withdrawal>> GetPendingWithdrawalsAsync(int count);

    /// <summary>
    /// Gets pending seller applications (ApplicationStatus.Pending).
    /// </summary>
    Task<IReadOnlyList<SellerProfile>> GetPendingSellerApplicationsAsync(int count);

    /// <summary>
    /// Gets pending rider applications (IsApproved = false).
    /// </summary>
    Task<IReadOnlyList<Rider>> GetPendingRiderApplicationsAsync(int count);

    // ════════════════════════════════════════════════════════════════════════════
    //  USER MANAGEMENT
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gets paginated users with optional search and role filtering.
    /// </summary>
    Task<(IReadOnlyList<User> Users, int TotalCount)> GetUsersAsync(
        string? searchTerm,
        string? roleFilter,
        int page,
        int pageSize);

    /// <summary>
    /// Updates a user's role. Returns false if user not found.
    /// </summary>
    Task<bool> UpdateUserRoleAsync(int userId, UserRole newRole);

    /// <summary>
    /// Toggles a user's account disabled status.
    /// </summary>
    Task<bool> ToggleUserStatusAsync(int userId, bool isDisabled);

    // ════════════════════════════════════════════════════════════════════════════
    //  SELLER APPROVALS
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gets seller applications, optionally filtered by status.
    /// </summary>
    Task<IReadOnlyList<SellerProfile>> GetSellerApplicationsAsync(string? statusFilter);

    /// <summary>
    /// Approves a seller application. Updates SellerProfile status to Approved
    /// and elevates User.Role to Seller. Returns false if application not found.
    /// </summary>
    Task<bool> ApproveSellerAsync(int applicationId);

    /// <summary>
    /// Rejects a seller application. Updates SellerProfile status to Rejected.
    /// User.Role remains unchanged.
    /// </summary>
    Task<bool> RejectSellerAsync(int applicationId);

    // ════════════════════════════════════════════════════════════════════════════
    //  RIDER APPROVALS
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gets rider applications, optionally filtered by approval status.
    /// </summary>
    Task<IReadOnlyList<Rider>> GetRiderApplicationsAsync(string? statusFilter);

    /// <summary>
    /// Approves a rider. Sets IsApproved = true.
    /// </summary>
    Task<bool> ApproveRiderAsync(int riderId);

    /// <summary>
    /// Rejects a rider. Sets IsApproved = false (they remain in the system
    /// but cannot accept deliveries). Admin can delete them if needed.
    /// </summary>
    Task<bool> RejectRiderAsync(int riderId);

    // ════════════════════════════════════════════════════════════════════════════
    //  WITHDRAWAL MANAGEMENT
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gets withdrawals, optionally filtered by status.
    /// </summary>
    Task<IReadOnlyList<Withdrawal>> GetWithdrawalsAsync(string? statusFilter);

    /// <summary>
    /// Marks a withdrawal as Processed (admin has started processing).
    /// </summary>
    Task<bool> MarkWithdrawalProcessedAsync(int withdrawalId);

    /// <summary>
    /// Marks a withdrawal as Completed (funds have been released).
    /// </summary>
    Task<bool> MarkWithdrawalCompletedAsync(int withdrawalId);

    // ════════════════════════════════════════════════════════════════════════════
    //  TRANSACTION OVERSIGHT
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gets paginated transactions across the entire platform with optional search.
    /// </summary>
    Task<(IReadOnlyList<Transaction> Transactions, int TotalCount)> GetAllTransactionsAsync(
        string? searchTerm,
        int page,
        int pageSize);

    // ════════════════════════════════════════════════════════════════════════════
    //  SYSTEM INFO
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns system information including counts, recent activity dates, etc.
    /// </summary>
    Task<SystemInfo> GetSystemInfoAsync();
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