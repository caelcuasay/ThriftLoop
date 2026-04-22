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
    /// <summary>Demo seed balance for new regular Users only. Riders start at ₱0.</summary>
    private const decimal UserSeedBalance = 0m;

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
    public async Task<Wallet> GetOrCreateWalletAsync(int userId)
    {
        var wallet = await _walletRepo.GetByUserIdAsync(userId);
        if (wallet is not null)
            return wallet;

        // First-time access — seed with demo balance.
        wallet = new Wallet
        {
            UserId = userId,
            RiderId = null,       // mutually exclusive with RiderId
            Balance = UserSeedBalance,
            PendingBalance = 0m,
            UpdatedAt = DateTime.UtcNow
        };

        await _walletRepo.AddAsync(wallet);

        // Audit the seed as a TopUp so the transaction history is clean.
        await _txRepo.AddAsync(new Transaction
        {
            OrderId = null,
            FromUserId = userId,
            ToUserId = userId,
            ToRiderId = null,
            Amount = UserSeedBalance,
            Type = TransactionType.TopUp,
            Status = TransactionStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        });

        _logger.LogInformation(
            "Wallet created for User {UserId} with ₱{SeedBalance} demo balance.",
            userId, UserSeedBalance);

        return wallet;
    }

    /// <inheritdoc />
    public async Task<Wallet> GetOrCreateRiderWalletAsync(int riderId)
    {
        var wallet = await _walletRepo.GetByRiderIdAsync(riderId);
        if (wallet is not null)
            return wallet;

        // Riders start with ₱0 — they earn through delivery fees.
        // RiderId is set; UserId is intentionally left null.
        wallet = new Wallet
        {
            RiderId = riderId,
            UserId = null,  // mutually exclusive with UserId
            Balance = 0m,
            PendingBalance = 0m,
            UpdatedAt = DateTime.UtcNow
        };

        await _walletRepo.AddAsync(wallet);

        _logger.LogInformation(
            "Wallet created for Rider {RiderId} with ₱0 starting balance.", riderId);

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

        // Records the full purchase amount (item price + delivery fee) as a single
        // deduction. This is the only buyer-side history entry for a wallet order —
        // PayRiderAsync intentionally omits a buyer-side record since it's already
        // captured here.
        await _txRepo.AddAsync(new Transaction
        {
            OrderId = orderId,
            FromUserId = buyerId,
            ToUserId = buyerId,
            ToRiderId = null,
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

        // Guard: clamp to avoid going negative if pending balance is off.
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
            ToRiderId = null,
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

    /// <inheritdoc />
    public async Task TransferWalletToWalletAsync(int orderId, int buyerId, int sellerId, decimal amount)
    {
        var buyerWallet = await GetOrCreateWalletAsync(buyerId);
        var sellerWallet = await GetOrCreateWalletAsync(sellerId);

        // Verify buyer has sufficient available balance
        if (buyerWallet.Balance < amount)
        {
            throw new InvalidOperationException(
                $"Insufficient wallet balance. Required: ₱{amount:N2}, Available: ₱{buyerWallet.Balance:N2}");
        }

        // Debit buyer's available balance
        buyerWallet.Balance -= amount;
        buyerWallet.UpdatedAt = DateTime.UtcNow;

        // Credit seller's balance
        sellerWallet.Balance += amount;
        sellerWallet.UpdatedAt = DateTime.UtcNow;

        await _walletRepo.UpdateAsync(buyerWallet);
        await _walletRepo.UpdateAsync(sellerWallet);

        // Record the wallet-to-wallet transaction
        await _txRepo.AddAsync(new Transaction
        {
            OrderId = orderId,
            FromUserId = buyerId,
            ToUserId = sellerId,
            ToRiderId = null,
            Amount = amount,
            Type = TransactionType.WalletPayment,
            Status = TransactionStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        });

        _logger.LogInformation(
            "WalletPayment ₱{Amount} from User {BuyerId} → User {SellerId} for Order {OrderId}.",
            amount, buyerId, sellerId, orderId);
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
            ToRiderId = null,
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

    // ── Rider delivery fee ─────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task PayRiderAsync(
        int orderId,
        int buyerId,
        int riderId,
        decimal deliveryFee,
        bool fromEscrow = false)
    {
        if (fromEscrow)
        {
            // Debit the remaining escrow (delivery-fee slice) from buyer's pending bucket.
            var buyerWallet = await GetOrCreateWalletAsync(buyerId);
            var actualDebit = Math.Min(deliveryFee, buyerWallet.PendingBalance);
            buyerWallet.PendingBalance -= actualDebit;
            buyerWallet.UpdatedAt = DateTime.UtcNow;
            await _walletRepo.UpdateAsync(buyerWallet);
        }

        // Credit the rider using the rider-specific wallet path (sets RiderId, not UserId).
        var riderWallet = await GetOrCreateRiderWalletAsync(riderId);
        riderWallet.Balance += deliveryFee;
        riderWallet.UpdatedAt = DateTime.UtcNow;
        await _walletRepo.UpdateAsync(riderWallet);

        // Create transaction record for the rider (uses ToRiderId, not ToUserId)
        await _txRepo.AddAsync(new Transaction
        {
            OrderId = orderId,
            FromUserId = buyerId,
            ToUserId = null,           // Not a user, so null
            ToRiderId = riderId,       // This is a rider
            Amount = deliveryFee,
            Type = TransactionType.DeliveryFeePayment,
            Status = TransactionStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        });

        _logger.LogInformation(
            "DeliveryFeePayment ₱{Amount} credited to Rider {RiderId} for Order {OrderId} " +
            "(fromEscrow={FromEscrow}).",
            deliveryFee, riderId, orderId, fromEscrow);
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
            ToRiderId = null,
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
            ToRiderId = null,
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