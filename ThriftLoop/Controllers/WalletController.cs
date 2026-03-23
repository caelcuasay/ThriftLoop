using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThriftLoop.Constants;
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
    private readonly ILogger<WalletController> _logger;

    public WalletController(
        IWalletService walletService,
        ITransactionRepository txRepo,
        IWithdrawalRepository withdrawalRepo,
        ILogger<WalletController> logger)
    {
        _walletService = walletService;
        _txRepo = txRepo;
        _withdrawalRepo = withdrawalRepo;
        _logger = logger;
    }

    // ── DASHBOARD ──────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = ResolveUserId();
        if (userId is null) return Unauthorized();

        var wallet = await _walletService.GetOrCreateWalletAsync(userId.Value);
        var transactions = await _txRepo.GetByUserIdAsync(userId.Value, take: WalletConstants.RecentTransactionCount);
        var withdrawals = await _withdrawalRepo.GetByUserIdAsync(userId.Value);

        var vm = new WalletIndexViewModel
        {
            Balance = wallet.Balance,
            PendingBalance = wallet.PendingBalance,
            RecentTransactions = transactions,
            Withdrawals = withdrawals,
            CurrentUserId = userId.Value
        };

        return View(vm);
    }

    // ── ADD FUNDS (demo top-up) ────────────────────────────────────────────

    [HttpGet]
    public IActionResult AddFunds() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddFunds(decimal amount)
    {
        var userId = ResolveUserId();
        if (userId is null) return Unauthorized();

        if (amount <= 0 || amount > WalletConstants.MaxTopUpAmount)
        {
            TempData["ErrorMessage"] =
                $"Please enter an amount between ₱1 and ₱{WalletConstants.MaxTopUpAmount:N0}.";
            return View();
        }

        await _walletService.TopUpAsync(userId.Value, amount);

        _logger.LogInformation(
            "User {UserId} added ₱{Amount} to wallet (demo).", userId.Value, amount);

        TempData["SuccessMessage"] = $"₱{amount:N2} has been added to your wallet.";
        return RedirectToAction(nameof(Index));
    }

    // ── WITHDRAW ───────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Withdraw()
    {
        var userId = ResolveUserId();
        if (userId is null) return Unauthorized();

        var wallet = await _walletService.GetOrCreateWalletAsync(userId.Value);
        ViewBag.AvailableBalance = wallet.Balance;

        return View(new WithdrawViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Withdraw(WithdrawViewModel vm)
    {
        var userId = ResolveUserId();
        if (userId is null) return Unauthorized();

        var wallet = await _walletService.GetOrCreateWalletAsync(userId.Value);
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
            userId.Value, vm.Amount, vm.Method, vm.Reference);

        if (!success)
        {
            TempData["ErrorMessage"] = "Withdrawal request could not be processed. Please try again.";
            return View(vm);
        }

        _logger.LogInformation(
            "User {UserId} requested withdrawal of ₱{Amount} via {Method}.",
            userId.Value, vm.Amount, vm.Method);

        TempData["SuccessMessage"] =
            $"Withdrawal of ₱{vm.Amount:N2} requested. We'll process it within 1–3 business days.";
        return RedirectToAction(nameof(Index));
    }
}