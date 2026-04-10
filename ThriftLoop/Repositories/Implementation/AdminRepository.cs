// Repositories/Implementation/AdminRepository.cs
using Microsoft.EntityFrameworkCore;
using ThriftLoop.Constants;
using ThriftLoop.Data;
using ThriftLoop.Enums;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;

namespace ThriftLoop.Repositories.Implementation;

public class AdminRepository : IAdminRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AdminRepository> _logger;

    public AdminRepository(ApplicationDbContext context, ILogger<AdminRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  DASHBOARD STATS
    // ════════════════════════════════════════════════════════════════════════════

    public async Task<DashboardStats> GetDashboardStatsAsync()
    {
        var totalUsers = await _context.Users.CountAsync();
        var totalSellers = await _context.Users.CountAsync(u => u.Role == UserRole.Seller);
        var totalRiders = await _context.Riders.CountAsync();
        var totalOrders = await _context.Orders.CountAsync();

        var totalRevenue = await _context.Orders
            .Where(o => o.Status == OrderStatus.Completed)
            .SumAsync(o => (decimal?)o.FinalPrice) ?? 0m;

        var pendingWithdrawals = await _context.Withdrawals
            .Where(w => w.Status == WithdrawalStatus.Requested)
            .SumAsync(w => (decimal?)w.Amount) ?? 0m;

        var pendingSellerApprovals = await _context.SellerProfiles
            .CountAsync(sp => sp.ApplicationStatus == ApplicationStatus.Pending);

        var pendingRiderApprovals = await _context.Riders
            .CountAsync(r => !r.IsApproved);

        return new DashboardStats
        {
            TotalUsers = totalUsers,
            TotalSellers = totalSellers,
            TotalRiders = totalRiders,
            TotalOrders = totalOrders,
            TotalRevenue = totalRevenue,
            PendingWithdrawals = pendingWithdrawals,
            PendingSellerApprovals = pendingSellerApprovals,
            PendingRiderApprovals = pendingRiderApprovals
        };
    }

    public async Task<IReadOnlyList<User>> GetRecentUsersAsync(int count)
        => await _context.Users
            .AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .Take(count)
            .Include(u => u.SellerProfile)
            .ToListAsync();

    public async Task<IReadOnlyList<Withdrawal>> GetPendingWithdrawalsAsync(int count)
        => await _context.Withdrawals
            .AsNoTracking()
            .Where(w => w.Status == WithdrawalStatus.Requested)
            .OrderBy(w => w.RequestedAt)
            .Take(count)
            .Include(w => w.User)
            .ToListAsync();

    public async Task<IReadOnlyList<SellerProfile>> GetPendingSellerApplicationsAsync(int count)
        => await _context.SellerProfiles
            .AsNoTracking()
            .Where(sp => sp.ApplicationStatus == ApplicationStatus.Pending)
            .OrderBy(sp => sp.AppliedAt)
            .Take(count)
            .Include(sp => sp.User)
            .ToListAsync();

    public async Task<IReadOnlyList<Rider>> GetPendingRiderApplicationsAsync(int count)
        => await _context.Riders
            .AsNoTracking()
            .Where(r => !r.IsApproved)
            .OrderBy(r => r.CreatedAt)
            .Take(count)
            .ToListAsync();

    // ════════════════════════════════════════════════════════════════════════════
    //  USER MANAGEMENT
    // ════════════════════════════════════════════════════════════════════════════

    public async Task<(IReadOnlyList<User> Users, int TotalCount)> GetUsersAsync(
        string? searchTerm,
        string? roleFilter,
        int page,
        int pageSize)
    {
        var query = _context.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchTerm))
            query = query.Where(u => u.Email.Contains(searchTerm));

        if (!string.IsNullOrWhiteSpace(roleFilter) &&
            Enum.TryParse<UserRole>(roleFilter, out var role))
        {
            query = query.Where(u => u.Role == role);
        }

        var totalCount = await query.CountAsync();

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(u => u.SellerProfile)
            .ToListAsync();

        return (users, totalCount);
    }

    public async Task<bool> UpdateUserRoleAsync(int userId, UserRole newRole)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user is null) return false;

        user.Role = newRole;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleUserStatusAsync(int userId, bool isDisabled)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user is null) return false;

        user.IsDisabled = isDisabled;
        user.DisabledAt = isDisabled ? DateTime.UtcNow : null;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "User {UserId} {Action}.", userId, isDisabled ? "disabled" : "re-enabled");

        return true;
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  SELLER APPROVALS
    // ════════════════════════════════════════════════════════════════════════════

    public async Task<IReadOnlyList<SellerProfile>> GetSellerApplicationsAsync(string? statusFilter)
    {
        var query = _context.SellerProfiles.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(statusFilter) &&
            Enum.TryParse<ApplicationStatus>(statusFilter, out var status))
        {
            query = query.Where(sp => sp.ApplicationStatus == status);
        }

        return await query
            .OrderByDescending(sp => sp.AppliedAt)
            .Include(sp => sp.User)
            .ToListAsync();
    }

    public async Task<SellerProfile?> GetSellerApplicationByIdAsync(int applicationId)
        => await _context.SellerProfiles
            .AsNoTracking()
            .Include(sp => sp.User)
            .FirstOrDefaultAsync(sp => sp.Id == applicationId);

    public async Task<bool> ApproveSellerAsync(int applicationId)
    {
        try
        {
            var application = await _context.SellerProfiles
                .FirstOrDefaultAsync(sp => sp.Id == applicationId);

            if (application is null ||
                application.ApplicationStatus != ApplicationStatus.Pending)
                return false;

            var user = await _context.Users.FindAsync(application.UserId);
            if (user is null) return false;

            application.ApplicationStatus = ApplicationStatus.Approved;
            application.ReviewedAt = DateTime.UtcNow;
            user.Role = UserRole.Seller;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Seller application {ApplicationId} approved — User {UserId} is now a Seller.",
                applicationId, application.UserId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to approve seller application {ApplicationId}.", applicationId);
            return false;
        }
    }

    public async Task<bool> RejectSellerAsync(int applicationId)
    {
        var application = await _context.SellerProfiles
            .FirstOrDefaultAsync(sp => sp.Id == applicationId);

        if (application is null ||
            application.ApplicationStatus != ApplicationStatus.Pending)
            return false;

        application.ApplicationStatus = ApplicationStatus.Rejected;
        application.ReviewedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Seller application {ApplicationId} rejected.", applicationId);

        return true;
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  RIDER APPROVALS - FIXED VERSION
    // ════════════════════════════════════════════════════════════════════════════

    // Repositories/Implementation/AdminRepository.cs - Update GetRiderApplicationsAsync

    // Repositories/Implementation/AdminRepository.cs

    public async Task<IReadOnlyList<Rider>> GetRiderApplicationsAsync(string? statusFilter)
    {
        var query = _context.Riders.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            switch (statusFilter.ToLower())
            {
                case "approved":
                    query = query.Where(r => r.IsApproved == true);
                    break;
                case "pending":
                    // Never rejected (no rejection reason) and not approved
                    query = query.Where(r => r.IsApproved == false &&
                                             string.IsNullOrEmpty(r.RejectionReason));
                    break;
                case "rejected":
                    // Has rejection reason, has NOT been resubmitted (ResubmittedAt is null)
                    query = query.Where(r => r.IsApproved == false &&
                                             !string.IsNullOrEmpty(r.RejectionReason) &&
                                             r.ResubmittedAt == null);
                    break;
                case "resubmitted":
                    // Has rejection reason AND has been resubmitted
                    query = query.Where(r => r.IsApproved == false &&
                                             !string.IsNullOrEmpty(r.RejectionReason) &&
                                             r.ResubmittedAt != null);
                    break;
                default:
                    // If status doesn't match, return all
                    break;
            }
        }

        return await query
            .OrderByDescending(r => r.ResubmittedAt ?? r.RejectedAt ?? r.CreatedAt)
            .ToListAsync();
    }

    public async Task<Rider?> GetRiderApplicationByIdAsync(int riderId)
        => await _context.Riders
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == riderId);

    public async Task<bool> ApproveRiderAsync(int riderId)
    {
        try
        {
            var rider = await _context.Riders.FindAsync(riderId);
            if (rider is null)
            {
                _logger.LogWarning("Rider {RiderId} not found.", riderId);
                return false;
            }

            // Check if already approved
            if (rider.IsApproved)
            {
                _logger.LogWarning("Rider {RiderId} is already approved.", riderId);
                return true;
            }

            // Approve the rider
            rider.IsApproved = true;

            // Create wallet for the approved rider if it doesn't exist
            var existingWallet = await _context.Wallets
                .FirstOrDefaultAsync(w => w.RiderId == riderId);

            if (existingWallet == null)
            {
                var wallet = new Wallet
                {
                    RiderId = riderId,
                    Balance = 0m,
                    PendingBalance = 0m,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.Wallets.Add(wallet);
                _logger.LogInformation("Created wallet for approved rider {RiderId}.", riderId);
            }
            else
            {
                _logger.LogInformation("Wallet already exists for rider {RiderId}.", riderId);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Rider {RiderId} approved successfully.", riderId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to approve rider {RiderId}.", riderId);
            return false;
        }
    }

    public async Task<bool> RejectRiderAsync(int riderId)
    {
        try
        {
            var rider = await _context.Riders.FindAsync(riderId);
            if (rider is null)
            {
                _logger.LogWarning("Rider {RiderId} not found.", riderId);
                return false;
            }

            // Only update if not already approved
            if (!rider.IsApproved)
            {
                rider.IsApproved = false;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Rider {RiderId} rejected.", riderId);
            }
            else
            {
                _logger.LogWarning("Cannot reject rider {RiderId} - already approved.", riderId);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reject rider {RiderId}.", riderId);
            return false;
        }
    }
    // ════════════════════════════════════════════════════════════════════════════
    //  WITHDRAWAL MANAGEMENT
    // ════════════════════════════════════════════════════════════════════════════

    public async Task<IReadOnlyList<Withdrawal>> GetWithdrawalsAsync(string? statusFilter)
    {
        var query = _context.Withdrawals.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(statusFilter) &&
            Enum.TryParse<WithdrawalStatus>(statusFilter, out var status))
        {
            query = query.Where(w => w.Status == status);
        }

        return await query
            .OrderByDescending(w => w.RequestedAt)
            .Include(w => w.User)
            .ToListAsync();
    }

    public async Task<bool> MarkWithdrawalProcessedAsync(int withdrawalId)
    {
        var w = await _context.Withdrawals.FindAsync(withdrawalId);
        if (w is null || w.Status != WithdrawalStatus.Requested) return false;

        w.Status = WithdrawalStatus.Processed;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Withdrawal {WithdrawalId} marked as processed.", withdrawalId);
        return true;
    }

    public async Task<bool> MarkWithdrawalCompletedAsync(int withdrawalId)
    {
        var w = await _context.Withdrawals.FindAsync(withdrawalId);
        if (w is null || w.Status != WithdrawalStatus.Processed) return false;

        w.Status = WithdrawalStatus.Completed;
        w.CompletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Withdrawal {WithdrawalId} marked as completed.", withdrawalId);
        return true;
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  TRANSACTION OVERSIGHT
    // ════════════════════════════════════════════════════════════════════════════

    public async Task<(IReadOnlyList<Transaction> Transactions, int TotalCount)>
        GetAllTransactionsAsync(string? searchTerm, int page, int pageSize)
    {
        var query = _context.Transactions.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(t =>
                (t.FromUser != null && t.FromUser.Email.Contains(searchTerm)) ||
                (t.ToUser != null && t.ToUser.Email.Contains(searchTerm)) ||
                (t.Order != null && t.Order.Id.ToString().Contains(searchTerm)));
        }

        var totalCount = await query.CountAsync();

        var transactions = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(t => t.FromUser)
            .Include(t => t.ToUser)
            .Include(t => t.ToRider)
            .Include(t => t.Order)
                .ThenInclude(o => o!.Item)
            .ToListAsync();

        return (transactions, totalCount);
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  SYSTEM INFO
    // ════════════════════════════════════════════════════════════════════════════

    public async Task<SystemInfo> GetSystemInfoAsync()
    {
        return new SystemInfo
        {
            TotalUsers = await _context.Users.CountAsync(),
            TotalSellers = await _context.Users.CountAsync(u => u.Role == UserRole.Seller),
            TotalRiders = await _context.Riders.CountAsync(),
            TotalItems = await _context.Items.CountAsync(),
            TotalOrders = await _context.Orders.CountAsync(),
            TotalDeliveries = await _context.Deliveries.CountAsync(),
            CompletedDeliveries = await _context.Deliveries.CountAsync(d => d.Status == DeliveryStatus.Completed),
            EarliestUserCreatedAt = await _context.Users
                .OrderBy(u => u.CreatedAt)
                .Select(u => (DateTime?)u.CreatedAt)
                .FirstOrDefaultAsync(),
            LatestOrderDate = await _context.Orders
                .OrderByDescending(o => o.OrderDate)
                .Select(o => (DateTime?)o.OrderDate)
                .FirstOrDefaultAsync(),
            TotalEscrowHeld = await _context.Wallets.SumAsync(w => w.PendingBalance),
            TotalPlatformBalance = await _context.Wallets.SumAsync(w => w.Balance)
        };
    }

    // Repositories/Implementation/AdminRepository.cs - Add new method
    // Add this method to the class:

    public async Task<bool> RejectRiderWithReasonAsync(int riderId, string reason)
    {
        try
        {
            var rider = await _context.Riders.FindAsync(riderId);
            if (rider is null)
            {
                _logger.LogWarning("Rider {RiderId} not found.", riderId);
                return false;
            }

            // Only reject if not already approved
            if (!rider.IsApproved)
            {
                rider.IsApproved = false;
                rider.RejectionReason = reason;
                rider.RejectedAt = DateTime.UtcNow;  // Set rejection timestamp
                rider.ResubmittedAt = null;  // Clear any previous resubmission timestamp
                rider.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Rider {RiderId} rejected with reason: {Reason}", riderId, reason);
                return true;
            }
            else
            {
                _logger.LogWarning("Cannot reject rider {RiderId} - already approved.", riderId);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reject rider {RiderId} with reason.", riderId);
            return false;
        }
    }
}