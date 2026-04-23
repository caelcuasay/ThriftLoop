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

namespace ThriftLoop.Controllers;

[Authorize]
public class WalletController : BaseController
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

    // ── DASHBOARD ──────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var id = ResolveUserId();
        if (id is null) return Unauthorized();

        var wallet = IsRider()
            ? await _walletService.GetOrCreateRiderWalletAsync(id.Value)
            : await _walletService.GetOrCreateWalletAsync(id.Value);

        IReadOnlyList<Transaction> transactions;
        if (IsRider())
        {
            transactions = await _txRepo.GetByRiderIdAsync(id.Value, take: WalletConstants.RecentTransactionCount);
        }
        else
        {
            transactions = await _txRepo.GetByUserIdAsync(id.Value, take: WalletConstants.RecentTransactionCount);
        }

        var withdrawals = await _withdrawalRepo.GetByUserIdAsync(id.Value);

        var vm = new WalletIndexViewModel
        {
            Balance = wallet.Balance,
            PendingBalance = wallet.PendingBalance,
            RecentTransactions = transactions,
            Withdrawals = withdrawals,
            CurrentUserId = id.Value
        };

        return View(vm);
    }

    // ── ADD FUNDS (GCash QR top-up) ─────────────────────────────────────────

    [HttpGet]
    [Authorize(Roles = "User,Seller")]
    public async Task<IActionResult> AddFunds()
    {
        var settings = await _context.SiteSettings.FirstOrDefaultAsync();
        var vm = new AddFundsViewModel
        {
            QRCodePath = settings?.GCashQRCodePath
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "User,Seller")]
    public async Task<IActionResult> AddFunds(IFormFile? receiptImage)
    {
        var userId = ResolveUserId();
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

        _logger.LogInformation("OCR Results - Ref: {Ref}, Amount: {Amount}, AccountNumber: {AccountNumber}",
            extractedRef, extractedAmount, extractedAccountNumber);

        var settings = await _context.SiteSettings.FirstOrDefaultAsync();
        var expectedAccountNumber = settings?.GCashAccountNumber;

        var normalizedExtracted = NormalizePhoneNumber(extractedAccountNumber);
        var normalizedExpected = NormalizePhoneNumber(expectedAccountNumber);

        bool accountNumberMatches = !string.IsNullOrEmpty(normalizedExpected) &&
                                    !string.IsNullOrEmpty(normalizedExtracted) &&
                                    normalizedExtracted == normalizedExpected;

        _logger.LogInformation("Account Number Check - Expected: {Expected} ({NormalizedExpected}), Got: {Got} ({NormalizedGot}), Matches: {Matches}",
            expectedAccountNumber, normalizedExpected, extractedAccountNumber, normalizedExtracted, accountNumberMatches);

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

            _logger.LogInformation("Top-up request {RequestId} for User {UserId} queued for manual review (OCR failed)",
                manualRequest.Id, userId.Value);

            TempData["SuccessMessage"] = "Receipt uploaded successfully. It will be reviewed by our team shortly.";
            return RedirectToAction(nameof(Index));
        }

        if (extractedAmount > WalletConstants.MaxTopUpAmount)
        {
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);

            TempData["ErrorMessage"] = $"Amount exceeds maximum limit of ₱{WalletConstants.MaxTopUpAmount:N0}.";
            return RedirectToAction(nameof(AddFunds));
        }

        var existing = await _context.TopUpRequests
            .FirstOrDefaultAsync(t => t.ReferenceNumber == extractedRef &&
                                      t.Status != TopUpStatus.Voided);

        if (existing != null)
        {
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);

            _logger.LogWarning("User {UserId} attempted duplicate top-up with ref {Ref}. Existing: {ExistingId}, Status: {Status}",
                userId.Value, extractedRef, existing.Id, existing.Status);

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
                bool meetsAmountThreshold = extractedAmount <= autoApproveThreshold;
                bool meetsConfidenceThreshold = ocrConfidence >= 0.7f;
                bool accountNumberValid = string.IsNullOrEmpty(normalizedExpected) || accountNumberMatches;

                _logger.LogInformation("Auto-approval check - Amount OK: {AmountOK} (₱{Amount} <= ₱{Threshold}), Confidence OK: {ConfidenceOK} ({Confidence} >= 0.7), Account OK: {AccountOK}",
                    meetsAmountThreshold, extractedAmount, autoApproveThreshold,
                    meetsConfidenceThreshold, ocrConfidence,
                    accountNumberValid);

                if (meetsAmountThreshold && meetsConfidenceThreshold && accountNumberValid)
                {
                    // AUTO-APPROVE: Credit wallet immediately
                    var wallet = await _context.Wallets
                        .FirstOrDefaultAsync(w => w.UserId == userId.Value);

                    if (wallet == null)
                    {
                        wallet = new Wallet
                        {
                            UserId = userId.Value,
                            Balance = extractedAmount,
                            PendingBalance = 0m,
                            UpdatedAt = DateTime.UtcNow
                        };
                        _context.Wallets.Add(wallet);
                    }
                    else
                    {
                        wallet.Balance += extractedAmount;
                        wallet.UpdatedAt = DateTime.UtcNow;
                    }

                    _context.Transactions.Add(new Transaction
                    {
                        OrderId = null,
                        FromUserId = userId.Value,
                        ToUserId = userId.Value,
                        ToRiderId = null,
                        Amount = extractedAmount,
                        Type = TransactionType.TopUp,
                        Status = TransactionStatus.Completed,
                        CreatedAt = DateTime.UtcNow,
                        CompletedAt = DateTime.UtcNow
                    });

                    request.Status = TopUpStatus.Approved;
                    request.ProcessedAt = DateTime.UtcNow;
                    request.ProcessedBy = ResolveUserId();
                    request.IsAutoApproved = true;
                    request.NeedsAdminReview = true; // Still needs admin to review/verify

                    await _context.SaveChangesAsync();

                    _logger.LogInformation("AUTO-APPROVED top-up request {RequestId} for User {UserId}, Amount ₱{Amount} (pending admin review)",
                        request.Id, userId.Value, extractedAmount);

                    TempData["SuccessMessage"] = $"₱{extractedAmount:N2} has been added to your wallet!";
                }
                else
                {
                    request.Status = TopUpStatus.NeedsReview;
                    request.NeedsAdminReview = true;
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Top-up request {RequestId} for User {UserId} requires manual review",
                        request.Id, userId.Value);

                    TempData["SuccessMessage"] = $"Top-up request submitted for ₱{extractedAmount:N2}. It will be reviewed shortly.";
                }
            });

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process top-up for User {UserId}", userId);

            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);

            TempData["ErrorMessage"] = "An error occurred while processing your top-up. Please try again.";
            return RedirectToAction(nameof(AddFunds));
        }
    }

    // ── OCR HELPER METHODS ─────────────────────────────────────────────────

    private async Task<(string? reference, decimal amount, string? accountNumber)> OcrExtractAsync(string imagePath)
    {
        try
        {
            var tessDataPath = Path.Combine(_environment.WebRootPath, "..", "tessdata");

            if (!Directory.Exists(tessDataPath))
            {
                _logger.LogWarning("Tesseract tessdata folder not found at {Path}. Using mock OCR.", tessDataPath);
                return await MockOcrFallbackAsync(imagePath);
            }

            using var engine = new Tesseract.TesseractEngine(tessDataPath, "eng", Tesseract.EngineMode.Default);
            using var img = Tesseract.Pix.LoadFromFile(imagePath);
            using var page = engine.Process(img);

            var text = page.GetText();
            var confidence = page.GetMeanConfidence();

            _logger.LogInformation("OCR Text extracted (confidence: {Confidence}%):\n{Text}",
                (int)(confidence * 100), text);

            var amount = ExtractAmountFromText(text);
            var reference = ExtractReferenceFromText(text);
            var accountNumber = ExtractAccountNumberFromText(text);

            return (reference, amount, accountNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OCR failed for image {ImagePath}", imagePath);
            return (null, 0m, null);
        }
    }

    private decimal ExtractAmountFromText(string text)
    {
        var patterns = new[]
        {
            @"Amount\s*[₱£P]\s*(\d+[.,]?\d*)",
            @"Total Amount Sent\s*[₱£P]\s*(\d+[.,]?\d*)",
            @"Total\s*[₱£P]\s*(\d+[.,]?\d*)",
            @"[₱£]\s*(\d+[.,]?\d*)",
            @"[₱£](\d+[.,]?\d*)",
            @"P(\d+[.,]?\d*)",
            @"PHP\s*(\d+[.,]?\d*)",
            @"Php\s*(\d+[.,]?\d*)",
        };

        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(text, pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success && decimal.TryParse(match.Groups[1].Value.Replace(",", ""), out var amount))
            {
                _logger.LogDebug("Extracted amount {Amount} using pattern: {Pattern}", amount, pattern);
                return amount;
            }
        }

        return 0m;
    }

    private string? ExtractReferenceFromText(string text)
    {
        var patterns = new[]
        {
            @"Ref\.?\s*No\.?\s*[:]?\s*(\d+)",
            @"Reference\s*No\.?\s*[:]?\s*(\d+)",
            @"Reference\s*[:]?\s*(\d+)",
            @"Ref#?\s*[:]?\s*(\d+)",
            @"Transaction\s*ID\s*[:]?\s*(\d+)",
            @"Receipt\s*No\.?\s*[:]?\s*(\d+)",
        };

        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(text, pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                _logger.LogDebug("Extracted reference {Ref} using pattern: {Pattern}", match.Groups[1].Value, pattern);
                return match.Groups[1].Value;
            }
        }

        var digitMatch = System.Text.RegularExpressions.Regex.Match(text, @"\b(\d{13})\b");
        if (digitMatch.Success)
        {
            _logger.LogDebug("Extracted reference {Ref} using 13-digit fallback", digitMatch.Groups[1].Value);
            return digitMatch.Groups[1].Value;
        }

        digitMatch = System.Text.RegularExpressions.Regex.Match(text, @"\b(\d{12})\b");
        if (digitMatch.Success)
        {
            _logger.LogDebug("Extracted reference {Ref} using 12-digit fallback", digitMatch.Groups[1].Value);
            return digitMatch.Groups[1].Value;
        }

        return null;
    }

    private string? ExtractAccountNumberFromText(string text)
    {
        _logger.LogDebug("Attempting to extract account number from text...");

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            _logger.LogDebug("Checking line: {Line}", line.Trim());

            var match = System.Text.RegularExpressions.Regex.Match(line, @"\+63\s*\d{3}\s*\d{3}\s*\d{4}");
            if (match.Success)
            {
                var rawNumber = match.Value;
                _logger.LogDebug("Found +63 number: {Raw}", rawNumber);

                var digits = new string(rawNumber.Where(char.IsDigit).ToArray());
                _logger.LogDebug("Digits extracted: {Digits}", digits);

                if (digits.StartsWith("63") && digits.Length >= 11)
                {
                    var number = "0" + digits[2..];
                    if (number.Length == 11)
                    {
                        _logger.LogDebug("Normalized to: {Number}", number);
                        return number;
                    }
                }
            }

            match = System.Text.RegularExpressions.Regex.Match(line, @"09\s*\d{3}\s*\d{3}\s*\d{4}");
            if (match.Success)
            {
                var rawNumber = match.Value;
                var digits = new string(rawNumber.Where(char.IsDigit).ToArray());
                if (digits.Length == 11 && digits.StartsWith("09"))
                {
                    _logger.LogDebug("Found 09 number: {Number}", digits);
                    return digits;
                }
            }
        }

        var textWithoutSpaces = text.Replace(" ", "");
        var fallbackMatch = System.Text.RegularExpressions.Regex.Match(textWithoutSpaces, @"\+639\d{9}");
        if (fallbackMatch.Success)
        {
            var number = "0" + fallbackMatch.Value[3..];
            _logger.LogDebug("Fallback extracted: {Number}", number);
            return number;
        }

        fallbackMatch = System.Text.RegularExpressions.Regex.Match(textWithoutSpaces, @"09\d{9}");
        if (fallbackMatch.Success)
        {
            _logger.LogDebug("Fallback extracted: {Number}", fallbackMatch.Value);
            return fallbackMatch.Value;
        }

        _logger.LogDebug("No account number found");
        return null;
    }

    private string? NormalizePhoneNumber(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return null;

        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());

        if (digits.StartsWith("63") && digits.Length >= 11)
        {
            return "0" + digits[2..];
        }

        return digits;
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
        var id = ResolveUserId();
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
        var id = ResolveUserId();
        if (id is null) return Unauthorized();

        var wallet = IsRider()
            ? await _walletService.GetOrCreateRiderWalletAsync(id.Value)
            : await _walletService.GetOrCreateWalletAsync(id.Value);

        ViewBag.AvailableBalance = wallet.Balance;

        if (!ModelState.IsValid)
            return View(vm);

        if (vm.Amount < WalletConstants.MinWithdrawalAmount)
        {
            ModelState.AddModelError(nameof(vm.Amount),
                $"Minimum withdrawal amount is ₱{WalletConstants.MinWithdrawalAmount:N0}.");
            return View(vm);
        }

        if (vm.Amount > wallet.Balance)
        {
            ModelState.AddModelError(nameof(vm.Amount),
                $"Insufficient balance. Your available balance is ₱{wallet.Balance:N2}.");
            return View(vm);
        }

        bool success = await _walletService.RequestWithdrawalAsync(
            id.Value, vm.Amount, vm.Method, vm.Reference);

        if (!success)
        {
            TempData["ErrorMessage"] = "Withdrawal request could not be processed. Please try again.";
            return View(vm);
        }

        _logger.LogInformation("User/Rider {Id} requested withdrawal of ₱{Amount} via {Method}.",
            id.Value, vm.Amount, vm.Method);

        TempData["SuccessMessage"] = $"Withdrawal of ₱{vm.Amount:N2} requested. We'll process it within 1–3 business days.";
        return RedirectToAction(nameof(Index));
    }
}