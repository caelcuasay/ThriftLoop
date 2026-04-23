// Controllers/AdminController.cs

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriftLoop.Data;
using ThriftLoop.Enums;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;
using ThriftLoop.ViewModels.Admin;
using Microsoft.AspNetCore.Hosting;

namespace ThriftLoop.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : BaseController
{
    private readonly IAdminRepository _adminRepo;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AdminController> _logger;
    private readonly IWebHostEnvironment _environment;

    public AdminController(
        IAdminRepository adminRepo,
        ApplicationDbContext context,
        ILogger<AdminController> logger,
        IWebHostEnvironment environment)
    {
        _adminRepo = adminRepo;
        _context = context;
        _logger = logger;
        _environment = environment;
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

        var referer = Request.Headers["Referer"].ToString();
        if (referer.Contains("RiderApplicationDetail"))
        {
            return RedirectToAction(nameof(RiderApplicationDetail), new { id = riderId });
        }

        return RedirectToAction(nameof(RiderApprovals));
    }

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

    // ════════════════════════════════════════════════════════════════════════════
    //  GCASH QR MANAGEMENT
    // ════════════════════════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> GCashQR()
    {
        var settings = await _context.SiteSettings.FirstOrDefaultAsync();
        return View(settings);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadGCashQR(IFormFile qrImage)
    {
        if (qrImage == null || qrImage.Length == 0)
        {
            TempData["ErrorMessage"] = "Please select a QR code image to upload.";
            return RedirectToAction(nameof(GCashQR));
        }

        var allowedTypes = new[] { "image/png", "image/jpeg", "image/jpg" };
        if (!allowedTypes.Contains(qrImage.ContentType.ToLower()))
        {
            TempData["ErrorMessage"] = "Only PNG or JPEG images are allowed.";
            return RedirectToAction(nameof(GCashQR));
        }

        if (qrImage.Length > 2 * 1024 * 1024)
        {
            TempData["ErrorMessage"] = "Image must be less than 2MB.";
            return RedirectToAction(nameof(GCashQR));
        }

        var fileName = $"gcash-qr-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8]}.png";
        var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "qr");

        if (!Directory.Exists(uploadsFolder))
            Directory.CreateDirectory(uploadsFolder);

        var filePath = Path.Combine(uploadsFolder, fileName);
        var webPath = $"/uploads/qr/{fileName}";

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await qrImage.CopyToAsync(stream);
        }

        var settings = await _context.SiteSettings.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new SiteSettings
            {
                GCashQRCodePath = webPath,
                QRCodeUpdatedAt = DateTime.UtcNow,
                QRCodeUpdatedBy = ResolveUserId()
            };
            _context.SiteSettings.Add(settings);
        }
        else
        {
            if (!string.IsNullOrEmpty(settings.GCashQRCodePath))
            {
                var oldPath = Path.Combine(_environment.WebRootPath, settings.GCashQRCodePath.TrimStart('/'));
                if (System.IO.File.Exists(oldPath))
                    System.IO.File.Delete(oldPath);
            }

            settings.GCashQRCodePath = webPath;
            settings.QRCodeUpdatedAt = DateTime.UtcNow;
            settings.QRCodeUpdatedBy = ResolveUserId();
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Admin {AdminId} uploaded new GCash QR code: {FilePath}",
            ResolveUserId(), webPath);

        TempData["SuccessMessage"] = "GCash QR code updated successfully.";
        return RedirectToAction(nameof(GCashQR));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveGCashQR()
    {
        var settings = await _context.SiteSettings.FirstOrDefaultAsync();
        if (settings?.GCashQRCodePath != null)
        {
            var filePath = Path.Combine(_environment.WebRootPath, settings.GCashQRCodePath.TrimStart('/'));
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);

            settings.GCashQRCodePath = null;
            settings.QRCodeUpdatedAt = DateTime.UtcNow;
            settings.QRCodeUpdatedBy = ResolveUserId();

            await _context.SaveChangesAsync();

            _logger.LogInformation("Admin {AdminId} removed GCash QR code.", ResolveUserId());
        }

        TempData["SuccessMessage"] = "GCash QR code removed.";
        return RedirectToAction(nameof(GCashQR));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateGCashAccountNumber(string accountNumber)
    {
        var settings = await _context.SiteSettings.FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(accountNumber))
        {
            if (settings != null)
            {
                settings.GCashAccountNumber = null;
                settings.AccountNumberUpdatedAt = DateTime.UtcNow;
                settings.AccountNumberUpdatedBy = ResolveUserId();
                await _context.SaveChangesAsync();

                _logger.LogInformation("Admin {AdminId} cleared GCash account number.", ResolveUserId());
                TempData["SuccessMessage"] = "GCash account number cleared.";
            }
            return RedirectToAction(nameof(GCashQR));
        }

        var normalized = accountNumber.Trim();
        if (normalized.StartsWith("+63"))
            normalized = "0" + normalized.Substring(3);
        else if (normalized.StartsWith("63"))
            normalized = "0" + normalized.Substring(2);

        if (!System.Text.RegularExpressions.Regex.IsMatch(normalized, @"^09\d{9}$"))
        {
            TempData["ErrorMessage"] = "Please enter a valid 11-digit Philippine mobile number (e.g., 09123456789).";
            return RedirectToAction(nameof(GCashQR));
        }

        if (settings == null)
        {
            settings = new SiteSettings
            {
                GCashAccountNumber = normalized,
                AccountNumberUpdatedAt = DateTime.UtcNow,
                AccountNumberUpdatedBy = ResolveUserId()
            };
            _context.SiteSettings.Add(settings);
        }
        else
        {
            settings.GCashAccountNumber = normalized;
            settings.AccountNumberUpdatedAt = DateTime.UtcNow;
            settings.AccountNumberUpdatedBy = ResolveUserId();
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Admin {AdminId} updated GCash account number to {Number}",
            ResolveUserId(), normalized);

        TempData["SuccessMessage"] = "GCash account number saved successfully.";
        return RedirectToAction(nameof(GCashQR));
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  TOP-UP REQUEST REVIEW
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Admin view for reviewing top-up requests with priority indicators.
    /// Shows Pending, NeedsReview, and Auto-Approved requests that still need review.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> TopUpRequests(TopUpStatus? status = null, int page = 1)
    {
        const int pageSize = 20;
        const decimal priorityThreshold = 300m;

        var query = _context.TopUpRequests
            .Include(t => t.User)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }
        else
        {
            // Default view: show Pending, NeedsReview, AND Approved requests that still need admin review
            query = query.Where(t =>
                t.Status == TopUpStatus.Pending ||
                t.Status == TopUpStatus.NeedsReview ||
                (t.Status == TopUpStatus.Approved && t.NeedsAdminReview == true));
        }

        var totalCount = await query.CountAsync();
        var requests = await query
            .OrderByDescending(t => t.Status == TopUpStatus.NeedsReview && t.Amount > priorityThreshold)
            .ThenByDescending(t => t.Status == TopUpStatus.NeedsReview)
            .ThenByDescending(t => t.Status == TopUpStatus.Pending)
            .ThenByDescending(t => t.Status == TopUpStatus.Approved && t.NeedsAdminReview == true)
            .ThenByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var vm = new TopUpReviewViewModel
        {
            Requests = requests,
            CurrentFilter = status,
            CurrentPage = page,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
            TotalCount = totalCount,
            PendingCount = await _context.TopUpRequests.CountAsync(t => t.Status == TopUpStatus.Pending),
            NeedsReviewCount = await _context.TopUpRequests.CountAsync(t => t.Status == TopUpStatus.NeedsReview),
            AutoApprovedPendingReviewCount = await _context.TopUpRequests
                .CountAsync(t => t.Status == TopUpStatus.Approved && t.NeedsAdminReview == true),
            HighValuePendingCount = await _context.TopUpRequests
                .CountAsync(t => (t.Status == TopUpStatus.Pending || t.Status == TopUpStatus.NeedsReview)
                              && t.Amount > priorityThreshold)
        };

        return View(vm);
    }

    /// <summary>
    /// Approve a top-up request after manual review.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveTopUpRequest(int id)
    {
        var request = await _context.TopUpRequests
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (request == null)
        {
            TempData["ErrorMessage"] = "Top-up request not found.";
            return RedirectToAction(nameof(TopUpRequests));
        }

        if (request.Status != TopUpStatus.Pending && request.Status != TopUpStatus.NeedsReview)
        {
            TempData["ErrorMessage"] = "This request has already been processed.";
            return RedirectToAction(nameof(TopUpRequests));
        }

        var wallet = await _context.Wallets
            .FirstOrDefaultAsync(w => w.UserId == request.UserId);

        if (wallet == null)
        {
            wallet = new Wallet
            {
                UserId = request.UserId,
                Balance = 0m,
                PendingBalance = 0m,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Wallets.Add(wallet);
        }

        wallet.Balance += request.Amount;
        wallet.UpdatedAt = DateTime.UtcNow;

        request.Status = TopUpStatus.Approved;
        request.ProcessedAt = DateTime.UtcNow;
        request.ProcessedBy = ResolveUserId();
        request.IsAutoApproved = false;
        request.NeedsAdminReview = false;

        _context.Transactions.Add(new Transaction
        {
            OrderId = null,
            FromUserId = request.UserId,
            ToUserId = request.UserId,
            ToRiderId = null,
            Amount = request.Amount,
            Type = TransactionType.TopUp,
            Status = TransactionStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Admin {AdminId} approved top-up request {RequestId} for User {UserId}, Amount {Amount}",
            ResolveUserId(), request.Id, request.UserId, request.Amount);

        TempData["SuccessMessage"] = $"Top-up request approved. ₱{request.Amount:N2} credited to user's wallet.";
        return RedirectToAction(nameof(TopUpRequests));
    }

    /// <summary>
    /// Review and approve a top-up request with manually entered/corrected amount and reference.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewAndApproveTopUp(int id, decimal amount, string referenceNumber)
    {
        if (amount <= 0)
        {
            TempData["ErrorMessage"] = "Please enter a valid amount greater than 0.";
            return RedirectToAction(nameof(TopUpRequests));
        }

        if (string.IsNullOrWhiteSpace(referenceNumber))
        {
            TempData["ErrorMessage"] = "Please enter the reference number from the receipt.";
            return RedirectToAction(nameof(TopUpRequests));
        }

        var request = await _context.TopUpRequests
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (request == null)
        {
            TempData["ErrorMessage"] = "Top-up request not found.";
            return RedirectToAction(nameof(TopUpRequests));
        }

        if (request.Status != TopUpStatus.Pending && request.Status != TopUpStatus.NeedsReview)
        {
            TempData["ErrorMessage"] = "This request has already been processed.";
            return RedirectToAction(nameof(TopUpRequests));
        }

        var existing = await _context.TopUpRequests
            .FirstOrDefaultAsync(t => t.ReferenceNumber == referenceNumber
                && t.Status == TopUpStatus.Approved
                && t.Id != id);

        if (existing != null)
        {
            TempData["ErrorMessage"] = "This reference number has already been used for another approved top-up.";
            return RedirectToAction(nameof(TopUpRequests));
        }

        request.Amount = amount;
        request.ReferenceNumber = referenceNumber;

        var wallet = await _context.Wallets
            .FirstOrDefaultAsync(w => w.UserId == request.UserId);

        if (wallet == null)
        {
            wallet = new Wallet
            {
                UserId = request.UserId,
                Balance = 0m,
                PendingBalance = 0m,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Wallets.Add(wallet);
        }

        wallet.Balance += amount;
        wallet.UpdatedAt = DateTime.UtcNow;

        request.Status = TopUpStatus.Approved;
        request.ProcessedAt = DateTime.UtcNow;
        request.ProcessedBy = ResolveUserId();
        request.IsAutoApproved = false;
        request.NeedsAdminReview = false;

        _context.Transactions.Add(new Transaction
        {
            OrderId = null,
            FromUserId = request.UserId,
            ToUserId = request.UserId,
            ToRiderId = null,
            Amount = amount,
            Type = TransactionType.TopUp,
            Status = TransactionStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Admin {AdminId} reviewed and approved top-up request {RequestId} for User {UserId}, Amount {Amount}, Ref {Ref}",
            ResolveUserId(), request.Id, request.UserId, amount, referenceNumber);

        TempData["SuccessMessage"] = $"Top-up request approved. ₱{amount:N2} credited to user's wallet.";
        return RedirectToAction(nameof(TopUpRequests));
    }

    /// <summary>
    /// Void a top-up request (transaction deemed invalid).
    /// If the request was already approved (auto or manual), deduct the amount from user's wallet.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VoidTopUpRequest(int id, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["ErrorMessage"] = "Please provide a reason for voiding this request.";
            return RedirectToAction(nameof(TopUpRequests));
        }

        var request = await _context.TopUpRequests
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (request == null)
        {
            TempData["ErrorMessage"] = "Top-up request not found.";
            return RedirectToAction(nameof(TopUpRequests));
        }

        if (request.Status == TopUpStatus.Voided)
        {
            TempData["ErrorMessage"] = "This request has already been voided.";
            return RedirectToAction(nameof(TopUpRequests));
        }

        bool wasApproved = request.Status == TopUpStatus.Approved;
        decimal amountToDeduct = request.Amount;

        if (wasApproved && amountToDeduct > 0)
        {
            var wallet = await _context.Wallets
                .FirstOrDefaultAsync(w => w.UserId == request.UserId);

            if (wallet != null)
            {
                if (wallet.Balance < amountToDeduct)
                {
                    _logger.LogWarning(
                        "User {UserId} has insufficient balance (₱{Balance}) to deduct voided amount ₱{Amount}. Setting balance to 0.",
                        request.UserId, wallet.Balance, amountToDeduct);
                    wallet.Balance = 0;
                }
                else
                {
                    wallet.Balance -= amountToDeduct;
                }

                wallet.UpdatedAt = DateTime.UtcNow;

                _context.Transactions.Add(new Transaction
                {
                    OrderId = null,
                    FromUserId = request.UserId,
                    ToUserId = request.UserId,
                    ToRiderId = null,
                    Amount = -amountToDeduct,
                    Type = TransactionType.TopUp,
                    Status = TransactionStatus.Completed,
                    CreatedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow
                });

                _logger.LogInformation(
                    "Deducted ₱{Amount} from User {UserId} wallet due to voided top-up request {RequestId}. New balance: ₱{NewBalance}",
                    amountToDeduct, request.UserId, request.Id, wallet.Balance);
            }
            else
            {
                _logger.LogWarning(
                    "No wallet found for User {UserId} when voiding approved top-up request {RequestId}",
                    request.UserId, request.Id);
            }
        }

        request.Status = TopUpStatus.Voided;
        request.ProcessedAt = DateTime.UtcNow;
        request.ProcessedBy = ResolveUserId();
        request.VoidReason = reason;
        request.NeedsAdminReview = false;

        await _context.SaveChangesAsync();

        string message;
        if (wasApproved)
        {
            message = $"Transaction voided and ₱{request.Amount:N2} deducted from user's wallet. Reason: {reason}";
        }
        else
        {
            message = $"Transaction voided. Reason: {reason}";
        }

        _logger.LogInformation(
            "Admin {AdminId} voided top-up request {RequestId}. Amount: {Amount}, WasApproved: {WasApproved}, Reason: {Reason}",
            ResolveUserId(), request.Id, request.Amount, wasApproved, reason);

        TempData["SuccessMessage"] = message;
        return RedirectToAction(nameof(TopUpRequests));
    }

    /// <summary>
    /// Reject a top-up request and provide reason.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectTopUpRequest(int id, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["ErrorMessage"] = "Please provide a reason for rejection.";
            return RedirectToAction(nameof(TopUpRequests));
        }

        var request = await _context.TopUpRequests.FindAsync(id);
        if (request == null)
        {
            TempData["ErrorMessage"] = "Top-up request not found.";
            return RedirectToAction(nameof(TopUpRequests));
        }

        if (request.Status != TopUpStatus.Pending && request.Status != TopUpStatus.NeedsReview)
        {
            TempData["ErrorMessage"] = "This request has already been processed.";
            return RedirectToAction(nameof(TopUpRequests));
        }

        request.Status = TopUpStatus.Rejected;
        request.ProcessedAt = DateTime.UtcNow;
        request.RejectionReason = reason;
        request.NeedsAdminReview = false;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Admin {AdminId} rejected top-up request {RequestId}. Reason: {Reason}",
            ResolveUserId(), request.Id, reason);

        TempData["SuccessMessage"] = "Top-up request rejected.";
        return RedirectToAction(nameof(TopUpRequests));
    }

    /// <summary>
    /// Mark an auto-approved top-up request as reviewed by admin.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkTopUpAsReviewed(int id)
    {
        var request = await _context.TopUpRequests.FindAsync(id);
        if (request == null)
        {
            TempData["ErrorMessage"] = "Top-up request not found.";
            return RedirectToAction(nameof(TopUpRequests));
        }

        request.NeedsAdminReview = false;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Admin {AdminId} marked top-up request {RequestId} as reviewed",
            ResolveUserId(), id);

        TempData["SuccessMessage"] = "Top-up request marked as reviewed.";
        return RedirectToAction(nameof(TopUpRequests));
    }
}