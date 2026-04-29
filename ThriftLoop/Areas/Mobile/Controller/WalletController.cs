// Areas/Mobile/Controllers/WalletController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriftLoop.Constants;
using ThriftLoop.Data;
using ThriftLoop.Enums;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;
using ThriftLoop.Services.WalletManagement.Interface;
using ThriftLoop.ViewModels;

namespace ThriftLoop.Areas.Mobile.Controllers;

[Area("Mobile")]
[Route("mobile/[controller]/[action]")]
[Authorize]
public class WalletController : Controller
{
    private readonly IWalletService _walletService;
    private readonly ITransactionRepository _txRepo;
    private readonly IWithdrawalRepository _withdrawalRepo;
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<WalletController> _logger;

    public WalletController(
        IWalletService walletService,
        ITransactionRepository txRepo,
        IWithdrawalRepository withdrawalRepo,
        ApplicationDbContext context,
        IWebHostEnvironment environment,
        ILogger<WalletController> logger)
    {
        _walletService = walletService;
        _txRepo = txRepo;
        _withdrawalRepo = withdrawalRepo;
        _context = context;
        _environment = environment;
        _logger = logger;
    }

    private int? GetUserId()
    {
        var raw = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(raw, out var id) ? id : null;
    }

    private bool IsRider() =>
        User.HasClaim(c => c.Type == "IsRider" && c.Value == "true");

    // ── DASHBOARD ──────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var id = GetUserId();
        if (id is null) return Unauthorized();

        var wallet = IsRider()
            ? await _walletService.GetOrCreateRiderWalletAsync(id.Value)
            : await _walletService.GetOrCreateWalletAsync(id.Value);

        IReadOnlyList<Transaction> transactions = IsRider()
            ? await _txRepo.GetByRiderIdAsync(id.Value, take: WalletConstants.RecentTransactionCount)
            : await _txRepo.GetByUserIdAsync(id.Value, take: WalletConstants.RecentTransactionCount);

        var withdrawals = await _withdrawalRepo.GetByUserIdAsync(id.Value);

        var vm = new WalletIndexViewModel
        {
            Balance = wallet.Balance,
            PendingBalance = wallet.PendingBalance,
            RecentTransactions = transactions,
            Withdrawals = withdrawals,
            CurrentUserId = id.Value
        };

        ViewBag.IsRider = IsRider();
        return View(vm);
    }

    // ── ADD FUNDS (GCash QR top-up) ───────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> AddFunds()
    {
        if (IsRider())
            return RedirectToAction(nameof(Index));

        var settings = await _context.SiteSettings.FirstOrDefaultAsync();
        var vm = new AddFundsViewModel
        {
            QRCodePath = settings?.GCashQRCodePath
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddFunds(IFormFile? receiptImage)
    {
        if (IsRider())
            return RedirectToAction(nameof(Index));

        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        if (receiptImage == null || receiptImage.Length == 0)
        {
            TempData["ErrorMessage"] = "Please upload a screenshot of your GCash receipt.";
            return RedirectToAction(nameof(AddFunds));
        }

        var allowedTypes = new[] { "image/png", "image/jpeg", "image/jpg" };
        if (!allowedTypes.Contains(receiptImage.ContentType.ToLower()))
        {
            TempData["ErrorMessage"] = "Only PNG or JPEG images are allowed.";
            return RedirectToAction(nameof(AddFunds));
        }

        if (receiptImage.Length > 5 * 1024 * 1024)
        {
            TempData["ErrorMessage"] = "Image must be less than 5MB.";
            return RedirectToAction(nameof(AddFunds));
        }

        var fileName = $"receipt-{userId.Value}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8]}.png";
        var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "receipts");
        if (!Directory.Exists(uploadsFolder))
            Directory.CreateDirectory(uploadsFolder);

        var filePath = Path.Combine(uploadsFolder, fileName);
        var webPath = $"/uploads/receipts/{fileName}";

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await receiptImage.CopyToAsync(stream);
        }

        var (extractedRef, extractedAmount, extractedAccountNumber) = await OcrExtractAsync(filePath);
        var ocrConfidence = extractedRef != null && extractedAmount > 0 ? 0.85f : 0.3f;

        _logger.LogInformation("Mobile OCR Results - Ref: {Ref}, Amount: {Amount}", extractedRef, extractedAmount);

        var settings = await _context.SiteSettings.FirstOrDefaultAsync();
        var expectedAccountNumber = settings?.GCashAccountNumber;
        var normalizedExtracted = NormalizePhoneNumber(extractedAccountNumber);
        var normalizedExpected = NormalizePhoneNumber(expectedAccountNumber);
        bool accountNumberMatches = !string.IsNullOrEmpty(normalizedExpected) &&
                                    !string.IsNullOrEmpty(normalizedExtracted) &&
                                    normalizedExtracted == normalizedExpected;

        if (string.IsNullOrEmpty(extractedRef) || extractedAmount <= 0)
        {
            var manualRef = $"MANUAL-{Guid.NewGuid().ToString("N")[..8]}";
            var manualRequest = new TopUpRequest
            {
                UserId = userId.Value,
                Amount = 0,
                ReferenceNumber = manualRef,
                Status = TopUpStatus.NeedsReview,
                ScreenshotPath = webPath,
                OcrConfidence = ocrConfidence,
                AccountNumberMatched = accountNumberMatches,
                NeedsAdminReview = true
            };
            _context.TopUpRequests.Add(manualRequest);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Receipt uploaded successfully. It will be reviewed by our team shortly.";
            return RedirectToAction(nameof(Index));
        }

        if (extractedAmount > WalletConstants.MaxTopUpAmount)
        {
            if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
            TempData["ErrorMessage"] = $"Amount exceeds maximum limit of ₱{WalletConstants.MaxTopUpAmount:N0}.";
            return RedirectToAction(nameof(AddFunds));
        }

        var existing = await _context.TopUpRequests
            .FirstOrDefaultAsync(t => t.ReferenceNumber == extractedRef && t.Status != TopUpStatus.Voided);
        if (existing != null)
        {
            if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
            TempData["ErrorMessage"] = "This receipt has already been used.";
            return RedirectToAction(nameof(AddFunds));
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                var request = new TopUpRequest
                {
                    UserId = userId.Value,
                    Amount = extractedAmount,
                    ReferenceNumber = extractedRef,
                    Status = TopUpStatus.Pending,
                    ScreenshotPath = webPath,
                    OcrConfidence = ocrConfidence,
                    AccountNumberMatched = accountNumberMatches,
                    NeedsAdminReview = true
                };
                _context.TopUpRequests.Add(request);
                await _context.SaveChangesAsync();

                const decimal autoApproveThreshold = 300m;
                bool meetsAmount = extractedAmount <= autoApproveThreshold;
                bool meetsConfidence = ocrConfidence >= 0.7f;
                bool accountOk = string.IsNullOrEmpty(normalizedExpected) || accountNumberMatches;

                if (meetsAmount && meetsConfidence && accountOk)
                {
                    var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId.Value);
                    if (wallet == null)
                    {
                        wallet = new Wallet { UserId = userId.Value, Balance = extractedAmount, PendingBalance = 0m, UpdatedAt = DateTime.UtcNow };
                        _context.Wallets.Add(wallet);
                    }
                    else
                    {
                        wallet.Balance += extractedAmount;
                        wallet.UpdatedAt = DateTime.UtcNow;
                    }

                    _context.Transactions.Add(new Transaction
                    {
                        FromUserId = userId.Value,
                        ToUserId = userId.Value,
                        Amount = extractedAmount,
                        Type = TransactionType.TopUp,
                        Status = TransactionStatus.Completed,
                        CreatedAt = DateTime.UtcNow,
                        CompletedAt = DateTime.UtcNow
                    });

                    request.Status = TopUpStatus.Approved;
                    request.ProcessedAt = DateTime.UtcNow;
                    request.ProcessedBy = userId.Value;
                    request.IsAutoApproved = true;
                    request.NeedsAdminReview = true;
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"₱{extractedAmount:N2} has been added to your wallet!";
                }
                else
                {
                    request.Status = TopUpStatus.NeedsReview;
                    request.NeedsAdminReview = true;
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Top-up request submitted for ₱{extractedAmount:N2}. It will be reviewed shortly.";
                }
            });

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process top-up for User {UserId}", userId);
            if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
            TempData["ErrorMessage"] = "An error occurred. Please try again.";
            return RedirectToAction(nameof(AddFunds));
        }
    }

    // ── OCR HELPERS ────────────────────────────────────────────────────────

    private async Task<(string? reference, decimal amount, string? accountNumber)> OcrExtractAsync(string imagePath)
    {
        try
        {
            var tessDataPath = Path.Combine(_environment.WebRootPath, "..", "tessdata");
            if (!Directory.Exists(tessDataPath)) return await MockOcrFallbackAsync(imagePath);

            using var engine = new Tesseract.TesseractEngine(tessDataPath, "eng", Tesseract.EngineMode.Default);
            using var img = Tesseract.Pix.LoadFromFile(imagePath);
            using var page = engine.Process(img);
            var text = page.GetText();
            return (ExtractReferenceFromText(text), ExtractAmountFromText(text), ExtractAccountNumberFromText(text));
        }
        catch { return (null, 0m, null); }
    }

    private decimal ExtractAmountFromText(string text)
    {
        var patterns = new[] { @"Amount\s*[₱£P]\s*(\d+[.,]?\d*)", @"Total\s*[₱£P]\s*(\d+[.,]?\d*)", @"[₱£]\s*(\d+[.,]?\d*)", @"PHP\s*(\d+[.,]?\d*)" };
        foreach (var p in patterns)
        {
            var m = System.Text.RegularExpressions.Regex.Match(text, p, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (m.Success && decimal.TryParse(m.Groups[1].Value.Replace(",", ""), out var amt)) return amt;
        }
        return 0m;
    }

    private string? ExtractReferenceFromText(string text)
    {
        var patterns = new[] { @"Ref\.?\s*No\.?\s*[:]?\s*(\d+)", @"Reference\s*[:]?\s*(\d+)", @"Transaction\s*ID\s*[:]?\s*(\d+)" };
        foreach (var p in patterns)
        {
            var m = System.Text.RegularExpressions.Regex.Match(text, p, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (m.Success) return m.Groups[1].Value;
        }
        return null;
    }

    private string? ExtractAccountNumberFromText(string text)
    {
        var m = System.Text.RegularExpressions.Regex.Match(text, @"09\d{9}");
        if (m.Success) return m.Value;
        m = System.Text.RegularExpressions.Regex.Match(text.Replace(" ", ""), @"\+639\d{9}");
        if (m.Success) return "0" + m.Value[3..];
        return null;
    }

    private string? NormalizePhoneNumber(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.StartsWith("63") && digits.Length >= 11 ? "0" + digits[2..] : digits;
    }

    private async Task<(string? reference, decimal amount, string? accountNumber)> MockOcrFallbackAsync(string imagePath)
    {
        await Task.Delay(1500);
        return (null, 0m, null);
    }

    // ── WITHDRAW ───────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Withdraw()
    {
        var id = GetUserId();
        if (id is null) return Unauthorized();

        var wallet = IsRider()
            ? await _walletService.GetOrCreateRiderWalletAsync(id.Value)
            : await _walletService.GetOrCreateWalletAsync(id.Value);

        ViewBag.AvailableBalance = wallet.Balance;
        return View(new WithdrawViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Withdraw(WithdrawViewModel vm)
    {
        var id = GetUserId();
        if (id is null) return Unauthorized();

        var wallet = IsRider()
            ? await _walletService.GetOrCreateRiderWalletAsync(id.Value)
            : await _walletService.GetOrCreateWalletAsync(id.Value);

        ViewBag.AvailableBalance = wallet.Balance;
        if (!ModelState.IsValid) return View(vm);

        if (vm.Amount < WalletConstants.MinWithdrawalAmount)
        { ModelState.AddModelError(nameof(vm.Amount), $"Minimum withdrawal is ₱{WalletConstants.MinWithdrawalAmount:N0}."); return View(vm); }
        if (vm.Amount > wallet.Balance)
        { ModelState.AddModelError(nameof(vm.Amount), $"Insufficient balance. Available: ₱{wallet.Balance:N2}."); return View(vm); }

        bool success = await _walletService.RequestWithdrawalAsync(id.Value, vm.Amount, vm.Method, vm.Reference);
        if (!success) { TempData["ErrorMessage"] = "Withdrawal request could not be processed."; return View(vm); }

        TempData["SuccessMessage"] = $"Withdrawal of ₱{vm.Amount:N2} requested. We'll process it within 1–3 business days.";
        return RedirectToAction(nameof(Index));
    }
}