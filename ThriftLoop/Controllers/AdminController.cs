// Controllers/AdminController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriftLoop.Data;
using ThriftLoop.Enums;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;
using ThriftLoop.ViewModels.Admin;

namespace ThriftLoop.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : BaseController
{
    private readonly IAdminRepository _adminRepo;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IAdminRepository adminRepo,
        ApplicationDbContext context,
        ILogger<AdminController> logger)
    {
        _adminRepo = adminRepo;
        _context = context;
        _logger = logger;
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  DASHBOARD
    // ════════════════════════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var stats = await _adminRepo.GetDashboardStatsAsync();
        var recentUsers = await _adminRepo.GetRecentUsersAsync(10);
        var recentWithdrawals = await _adminRepo.GetPendingWithdrawalsAsync(10);
        var recentSellerApps = await _adminRepo.GetPendingSellerApplicationsAsync(5);
        var recentRiderApps = await _adminRepo.GetPendingRiderApplicationsAsync(5);

        var vm = new AdminDashboardViewModel
        {
            TotalUsers = stats.TotalUsers,
            TotalSellers = stats.TotalSellers,
            TotalRiders = stats.TotalRiders,
            TotalOrders = stats.TotalOrders,
            TotalRevenue = stats.TotalRevenue,
            PendingWithdrawals = stats.PendingWithdrawals,
            PendingSellerApprovals = stats.PendingSellerApprovals,
            PendingRiderApprovals = stats.PendingRiderApprovals,
            RecentUsers = recentUsers,
            RecentWithdrawals = recentWithdrawals,
            RecentSellerApplications = recentSellerApps,
            RecentRiderApplications = recentRiderApps
        };

        return View(vm);
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  USER MANAGEMENT
    // ════════════════════════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> Users(string? search, string? role, int page = 1)
    {
        const int pageSize = 20;
        var (users, totalCount) = await _adminRepo.GetUsersAsync(search, role, page, pageSize);

        var vm = new UserManagementViewModel
        {
            Users = users,
            SearchTerm = search,
            RoleFilter = role,
            CurrentPage = page,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
            TotalCount = totalCount
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateUserRole(int userId, UserRole newRole)
    {
        var success = await _adminRepo.UpdateUserRoleAsync(userId, newRole);

        if (!success)
            TempData["ErrorMessage"] = "Failed to update user role. User not found.";
        else
        {
            _logger.LogInformation(
                "Admin updated User {UserId} role to {NewRole}.", userId, newRole);
            TempData["SuccessMessage"] = $"User role updated to {newRole}.";
        }

        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleUserStatus(int userId, bool isDisabled)
    {
        var success = await _adminRepo.ToggleUserStatusAsync(userId, isDisabled);

        if (!success)
            TempData["ErrorMessage"] = "Failed to update user status.";
        else
        {
            _logger.LogInformation(
                "Admin {Action} User {UserId}.",
                isDisabled ? "disabled" : "enabled", userId);
            TempData["SuccessMessage"] =
                $"User {(isDisabled ? "disabled" : "enabled")} successfully.";
        }

        return RedirectToAction(nameof(Users));
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  SELLER APPROVALS
    // ════════════════════════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> SellerApprovals(string? status = null)
    {
        var applications = await _adminRepo.GetSellerApplicationsAsync(status);
        return View(applications);
    }

    /// <summary>
    /// Detail page — lets the admin review StoreAddress, GovIdUrl, ShopName, Bio,
    /// and the applicant's account info before deciding to approve or reject.
    /// Maps to Views/Admin/SellerApplicationDetail.cshtml.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> SellerApplicationDetail(int id)
    {
        var application = await _adminRepo.GetSellerApplicationByIdAsync(id);

        if (application is null)
        {
            TempData["ErrorMessage"] = "Seller application not found.";
            return RedirectToAction(nameof(SellerApprovals));
        }

        return View(application);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveSeller(int applicationId)
    {
        var success = await _adminRepo.ApproveSellerAsync(applicationId);

        if (!success)
            TempData["ErrorMessage"] = "Failed to approve seller application.";
        else
        {
            _logger.LogInformation(
                "Admin approved seller application {ApplicationId}.", applicationId);
            TempData["SuccessMessage"] =
                "Seller application approved. The user can now manage their shop.";
        }

        return RedirectToAction(nameof(SellerApprovals));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectSeller(int applicationId)
    {
        var success = await _adminRepo.RejectSellerAsync(applicationId);

        if (!success)
            TempData["ErrorMessage"] = "Failed to reject seller application.";
        else
        {
            _logger.LogInformation(
                "Admin rejected seller application {ApplicationId}.", applicationId);
            TempData["SuccessMessage"] = "Seller application rejected.";
        }

        return RedirectToAction(nameof(SellerApprovals));
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  RIDER APPROVALS
    // ════════════════════════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> RiderApprovals(string? status = null)
    {
        var applications = await _adminRepo.GetRiderApplicationsAsync(status);
        return View(applications);
    }

    /// <summary>
    /// Detail page — lets the admin review rider's full application details
    /// including driver's license photo, vehicle info, and address before deciding.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> RiderApplicationDetail(int id)
    {
        var application = await _adminRepo.GetRiderApplicationByIdAsync(id);

        if (application is null)
        {
            TempData["ErrorMessage"] = "Rider application not found.";
            return RedirectToAction(nameof(RiderApprovals));
        }

        return View(application);
    }

    // Controllers/AdminController.cs - Rider Approval Actions

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveRider(int riderId)
    {
        var success = await _adminRepo.ApproveRiderAsync(riderId);

        if (!success)
        {
            TempData["ErrorMessage"] = "Failed to approve rider. Please try again.";
            _logger.LogWarning("Failed to approve rider {RiderId}.", riderId);
        }
        else
        {
            _logger.LogInformation("Admin approved rider {RiderId}.", riderId);
            TempData["SuccessMessage"] = "Rider approved. They can now accept delivery jobs.";
        }

        // Redirect back to the detail page if coming from there, otherwise to the list
        var referer = Request.Headers["Referer"].ToString();
        if (referer.Contains("RiderApplicationDetail"))
        {
            return RedirectToAction(nameof(RiderApplicationDetail), new { id = riderId });
        }

        return RedirectToAction(nameof(RiderApprovals));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectRider(int riderId)
    {
        var success = await _adminRepo.RejectRiderAsync(riderId);

        if (!success)
        {
            TempData["ErrorMessage"] = "Failed to reject rider. Please try again.";
            _logger.LogWarning("Failed to reject rider {RiderId}.", riderId);
        }
        else
        {
            _logger.LogInformation("Admin rejected rider {RiderId}.", riderId);
            TempData["SuccessMessage"] = "Rider rejected.";
        }

        // Redirect back to the detail page if coming from there, otherwise to the list
        var referer = Request.Headers["Referer"].ToString();
        if (referer.Contains("RiderApplicationDetail"))
        {
            return RedirectToAction(nameof(RiderApplicationDetail), new { id = riderId });
        }

        return RedirectToAction(nameof(RiderApprovals));
    }

    // Controllers/AdminController.cs - Add new action

    [HttpGet]
    public async Task<IActionResult> RejectRiderWithReason(int id)
    {
        var rider = await _adminRepo.GetRiderApplicationByIdAsync(id);
        if (rider is null)
        {
            TempData["ErrorMessage"] = "Rider application not found.";
            return RedirectToAction(nameof(RiderApprovals));
        }

        var model = new ThriftLoop.DTOs.Admin.RejectRiderDto
        {
            RiderId = rider.Id
        };

        ViewBag.RiderName = rider.FullName;
        ViewBag.RiderEmail = rider.Email;

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectRiderWithReason(ThriftLoop.DTOs.Admin.RejectRiderDto model)
    {
        if (!ModelState.IsValid)
        {
            var rider = await _adminRepo.GetRiderApplicationByIdAsync(model.RiderId);
            ViewBag.RiderName = rider?.FullName;
            ViewBag.RiderEmail = rider?.Email;
            return View(model);
        }

        var success = await _adminRepo.RejectRiderWithReasonAsync(model.RiderId, model.Reason);

        if (!success)
        {
            TempData["ErrorMessage"] = "Failed to reject rider. Please try again.";
            _logger.LogWarning("Failed to reject rider {RiderId}.", model.RiderId);
        }
        else
        {
            _logger.LogInformation("Admin rejected rider {RiderId} with reason: {Reason}", model.RiderId, model.Reason);
            TempData["SuccessMessage"] = $"Rider rejected. Reason: {model.Reason}";
        }

        return RedirectToAction(nameof(RiderApprovals));
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  WITHDRAWAL MANAGEMENT
    // ════════════════════════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> Withdrawals(string? status = null)
    {
        var withdrawals = await _adminRepo.GetWithdrawalsAsync(status);
        return View(withdrawals);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessWithdrawal(int withdrawalId)
    {
        var success = await _adminRepo.MarkWithdrawalProcessedAsync(withdrawalId);

        if (!success)
            TempData["ErrorMessage"] = "Failed to mark withdrawal as processed.";
        else
        {
            _logger.LogInformation(
                "Admin marked withdrawal {WithdrawalId} as processed.", withdrawalId);
            TempData["SuccessMessage"] = "Withdrawal marked as processed.";
        }

        return RedirectToAction(nameof(Withdrawals));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteWithdrawal(int withdrawalId)
    {
        var success = await _adminRepo.MarkWithdrawalCompletedAsync(withdrawalId);

        if (!success)
            TempData["ErrorMessage"] = "Failed to mark withdrawal as completed.";
        else
        {
            _logger.LogInformation(
                "Admin marked withdrawal {WithdrawalId} as completed.", withdrawalId);
            TempData["SuccessMessage"] = "Withdrawal marked as completed.";
        }

        return RedirectToAction(nameof(Withdrawals));
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  TRANSACTION OVERSIGHT
    // ════════════════════════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> Transactions(string? search, int page = 1)
    {
        const int pageSize = 30;
        var (transactions, totalCount) =
            await _adminRepo.GetAllTransactionsAsync(search, page, pageSize);

        var vm = new TransactionOversightViewModel
        {
            Transactions = transactions,
            SearchTerm = search,
            CurrentPage = page,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
            TotalCount = totalCount
        };

        return View(vm);
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  SYSTEM ACTIONS
    // ════════════════════════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> SystemInfo()
    {
        var info = await _adminRepo.GetSystemInfoAsync();
        return View(info);
    }
}