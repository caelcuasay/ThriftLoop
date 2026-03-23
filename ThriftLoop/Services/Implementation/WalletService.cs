using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;
using ThriftLoop.Services.WalletManagement.Interface;
using ThriftLoop.Enums;

namespace ThriftLoop.Services.WalletManagement.Implementation;

/// <summary>
/// Orchestrates all wallet operations. Each public method is a single
/// logical unit — it reads wallets, mutates balances, persists both the
/// updated wallet(s) and an audit Transaction in the correct order.
///
/// Concurrency note: for this demo implementation we use simple sequential
/// EF saves. A production system would wrap mutations in a DB transaction
/// with appropriate isolation or use optimistic concurrency tokens.
/// </summary>
public class WalletService : IWalletService
{
    private const decimal SeedBalance = 1_000m;

    private readonly IWalletRepository _walletRepo;
    private readonly ITransactionRepository _txRepo;
    private readonly IWithdrawalRepository _withdrawalRepo;
    private readonly ILogger<WalletService> _logger;

    public WalletService(
        IWalletRepository walletRepo,
        ITransactionRepository txRepo,
        IWithdrawalRepository withdrawalRepo,
        ILogger<WalletService> logger)
    {
        _walletRepo = walletRepo;
        _txRepo = txRepo;
        _withdrawalRepo = withdrawalRepo;
        _logger = logger;
    }

    // ── Wallet access ──────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<Models.Wallet> GetOrCreateWalletAsync(int userId)
    {
        var wallet = await _walletRepo.GetByUserIdAsync(userId);
        if (wallet is not null)
            return wallet;

        // First-time access — seed with demo balance.
        wallet = new Models.Wallet
        {
            UserId = userId,
            Balance = SeedBalance,
            PendingBalance = 0m,
            UpdatedAt = DateTime.UtcNow
        };

        await _walletRepo.AddAsync(wallet);

        // Audit the seed as a TopUp so the history is clean.
        await _txRepo.AddAsync(new Transaction
        {
            OrderId = null,
            FromUserId = userId,
            ToUserId = userId,
            Amount = SeedBalance,
            Type = TransactionType.TopUp,
            Status = TransactionStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        });

        _logger.LogInformation(
            "Wallet created for User {UserId} with ₱{SeedBalance} demo balance.",
            userId, SeedBalance);

        return wallet;
    }

    // ── Escrow ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<bool> HoldEscrowAsync(int orderId, int buyerId, decimal amount)
    {
        var wallet = await GetOrCreateWalletAsync(buyerId);

        if (wallet.Balance < amount)
        {
            _logger.LogWarning(
                "EscrowHold failed — User {BuyerId} has ₱{Balance} but needs ₱{Amount} for Order {OrderId}.",
                buyerId, wallet.Balance, amount, orderId);
            return false;
        }

        wallet.Balance -= amount;
        wallet.PendingBalance += amount;
        wallet.UpdatedAt = DateTime.UtcNow;

        await _walletRepo.UpdateAsync(wallet);

        await _txRepo.AddAsync(new Transaction
        {
            OrderId = orderId,
            FromUserId = buyerId,
            ToUserId = buyerId,  // money stays with buyer (in their pending bucket)
            Amount = amount,
            Type = TransactionType.EscrowHold,
            Status = TransactionStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        });

        _logger.LogInformation(
            "EscrowHold ₱{Amount} for User {BuyerId} on Order {OrderId}. " +
            "New balance: ₱{Balance} available, ₱{Pending} pending.",
            amount, buyerId, orderId, wallet.Balance, wallet.PendingBalance);

        return true;
    }

    /// <inheritdoc />
    public async Task ReleaseEscrowAsync(int orderId, int buyerId, int sellerId, decimal amount)
    {
        var buyerWallet = await GetOrCreateWalletAsync(buyerId);
        var sellerWallet = await GetOrCreateWalletAsync(sellerId);

        // Guard: pending balance should cover the release. Clamp to avoid going negative.
        var actualRelease = Math.Min(amount, buyerWallet.PendingBalance);

        buyerWallet.PendingBalance -= actualRelease;
        buyerWallet.UpdatedAt = DateTime.UtcNow;

        sellerWallet.Balance += actualRelease;
        sellerWallet.UpdatedAt = DateTime.UtcNow;

        await _walletRepo.UpdateAsync(buyerWallet);
        await _walletRepo.UpdateAsync(sellerWallet);

        await _txRepo.AddAsync(new Transaction
        {
            OrderId = orderId,
            FromUserId = buyerId,
            ToUserId = sellerId,
            Amount = actualRelease,
            Type = TransactionType.EscrowRelease,
            Status = TransactionStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        });

        _logger.LogInformation(
            "EscrowRelease ₱{Amount} from User {BuyerId} → User {SellerId} for Order {OrderId}.",
            actualRelease, buyerId, sellerId, orderId);
    }

    // ── Cash on Delivery ───────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task RecordCashCollectionAsync(int orderId, int buyerId, int sellerId, decimal amount)
    {
        var sellerWallet = await GetOrCreateWalletAsync(sellerId);

        sellerWallet.Balance += amount;
        sellerWallet.UpdatedAt = DateTime.UtcNow;

        await _walletRepo.UpdateAsync(sellerWallet);

        await _txRepo.AddAsync(new Transaction
        {
            OrderId = orderId,
            FromUserId = buyerId,
            ToUserId = sellerId,
            Amount = amount,
            Type = TransactionType.CashCollection,
            Status = TransactionStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        });

        _logger.LogInformation(
            "CashCollection ₱{Amount} credited to User {SellerId} for Order {OrderId}.",
            amount, sellerId, orderId);
    }

    // ── Top-up ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task TopUpAsync(int userId, decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Top-up amount must be positive.");

        var wallet = await GetOrCreateWalletAsync(userId);

        wallet.Balance += amount;
        wallet.UpdatedAt = DateTime.UtcNow;

        await _walletRepo.UpdateAsync(wallet);

        await _txRepo.AddAsync(new Transaction
        {
            OrderId = null,
            FromUserId = userId,
            ToUserId = userId,
            Amount = amount,
            Type = TransactionType.TopUp,
            Status = TransactionStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        });

        _logger.LogInformation("TopUp ₱{Amount} credited to User {UserId}.", amount, userId);
    }

    // ── Withdrawal ─────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<bool> RequestWithdrawalAsync(
        int userId, decimal amount, WithdrawalMethod method, string? reference)
    {
        if (amount <= 0)
            return false;

        var wallet = await GetOrCreateWalletAsync(userId);

        if (wallet.Balance < amount)
        {
            _logger.LogWarning(
                "Withdrawal request failed — User {UserId} has ₱{Balance} but requested ₱{Amount}.",
                userId, wallet.Balance, amount);
            return false;
        }

        wallet.Balance -= amount;
        wallet.UpdatedAt = DateTime.UtcNow;

        await _walletRepo.UpdateAsync(wallet);

        var withdrawal = new Withdrawal
        {
            UserId = userId,
            Amount = amount,
            Method = method,
            Status = WithdrawalStatus.Requested,
            Reference = reference,
            RequestedAt = DateTime.UtcNow
        };

        await _withdrawalRepo.AddAsync(withdrawal);

        await _txRepo.AddAsync(new Transaction
        {
            OrderId = null,
            FromUserId = userId,
            ToUserId = userId,
            Amount = amount,
            Type = TransactionType.Withdrawal,
            Status = TransactionStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        });

        _logger.LogInformation(
            "Withdrawal request #{WithdrawalId} — User {UserId} requested ₱{Amount} via {Method}.",
            withdrawal.Id, userId, amount, method);

        return true;
    }
}