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
    /// <remarks>
    /// Releases the item-price portion of the escrow to the seller.
    /// The delivery-fee portion is released separately via
    /// <see cref="PayRiderAsync"/> with <c>fromEscrow = true</c>.
    /// Pass <c>amount = order.FinalPrice − order.DeliveryFee</c> so that
    /// only the item price is credited here.
    /// </remarks>
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
    /// <remarks>
    /// Pass <c>amount = order.FinalPrice − order.DeliveryFee</c> so that
    /// only the item price is credited to the seller. The delivery fee is
    /// credited to the rider separately via <see cref="PayRiderAsync"/>
    /// with <c>fromEscrow = false</c>.
    /// </remarks>
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

    // ── Rider delivery fee ─────────────────────────────────────────────────

    /// <inheritdoc />
    /// <remarks>
    /// Credits the flat delivery fee to the rider's wallet after a successful delivery.
    ///
    /// Wallet orders  (<c>fromEscrow = true</c>):
    ///   Debits the buyer's PendingBalance (the escrow bucket) by
    ///   <paramref name="deliveryFee"/> and credits the rider's Balance.
    ///   Call this after <see cref="ReleaseEscrowAsync"/> so the two debits
    ///   together drain the full escrow hold.
    ///
    /// COD orders (<c>fromEscrow = false</c>):
    ///   The buyer already paid the rider in cash. This call only records the
    ///   wallet credit so the rider's earnings history is accurate.
    ///   The buyer's wallet is not touched.
    /// </remarks>
    public async Task PayRiderAsync(
        int orderId,
        int buyerId,
        int riderUserId,
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

        // Credit the rider.
        var riderWallet = await GetOrCreateWalletAsync(riderUserId);
        riderWallet.Balance += deliveryFee;
        riderWallet.UpdatedAt = DateTime.UtcNow;
        await _walletRepo.UpdateAsync(riderWallet);

        await _txRepo.AddAsync(new Transaction
        {
            OrderId = orderId,
            FromUserId = buyerId,
            ToUserId = riderUserId,
            Amount = deliveryFee,
            Type = TransactionType.DeliveryFeePayment,
            Status = TransactionStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        });

        _logger.LogInformation(
            "DeliveryFeePayment ₱{Amount} credited to Rider User {RiderUserId} for Order {OrderId} " +
            "(fromEscrow={FromEscrow}).",
            deliveryFee, riderUserId, orderId, fromEscrow);
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