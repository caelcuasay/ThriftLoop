using ThriftLoop.Enums;
using ThriftLoop.Models;

namespace ThriftLoop.Services.WalletManagement.Interface;

public interface IWalletService
{
    /// <summary>
    /// Returns the wallet for <paramref name="userId"/> (dbo.Users), creating
    /// and seeding it with a demo balance if one does not yet exist.
    /// </summary>
    Task<Wallet> GetOrCreateWalletAsync(int userId);

    /// <summary>
    /// Returns the wallet for <paramref name="riderId"/> (dbo.Riders), creating
    /// it with a zero balance if one does not yet exist.
    /// Riders and Users are separate tables — this sets Wallet.RiderId, not UserId.
    /// </summary>
    Task<Wallet> GetOrCreateRiderWalletAsync(int riderId);

    /// <summary>
    /// Moves <paramref name="amount"/> from the buyer's available Balance into
    /// their PendingBalance (escrow hold). The full amount includes the item
    /// price and the flat delivery fee.
    /// Returns <c>false</c> if the buyer has insufficient funds.
    /// </summary>
    Task<bool> HoldEscrowAsync(int orderId, int buyerId, decimal amount);

    /// <summary>
    /// Releases the item-price portion of the escrow to the seller.
    /// Pass <c>amount = order.FinalPrice − order.DeliveryFee</c>.
    /// The delivery-fee slice is released separately via
    /// <see cref="PayRiderAsync"/> with <c>fromEscrow = true</c>.
    /// </summary>
    Task ReleaseEscrowAsync(int orderId, int buyerId, int sellerId, decimal amount);

    /// <summary>
    /// Records a cash-on-delivery payment to the seller's wallet.
    /// Pass <c>amount = order.FinalPrice − order.DeliveryFee</c> (item price only).
    /// The delivery fee is credited to the rider separately via
    /// <see cref="PayRiderAsync"/> with <c>fromEscrow = false</c>.
    /// </summary>
    Task RecordCashCollectionAsync(int orderId, int buyerId, int sellerId, decimal amount);

    /// <summary>
    /// Credits the flat delivery fee to the rider's wallet after a successful delivery.
    /// <paramref name="riderId"/> is a Riders.Id — NOT a Users.Id.
    ///
    /// <paramref name="fromEscrow"/> = <c>true</c>  (Wallet orders):
    ///   Also debits the buyer's PendingBalance by <paramref name="deliveryFee"/>.
    ///   Call after <see cref="ReleaseEscrowAsync"/> to fully drain the escrow.
    ///
    /// <paramref name="fromEscrow"/> = <c>false</c> (COD orders):
    ///   The buyer already paid in cash. Only the rider's Balance is updated.
    /// </summary>
    Task PayRiderAsync(
        int orderId,
        int buyerId,
        int riderId,
        decimal deliveryFee,
        bool fromEscrow = false);

    /// <summary>Adds <paramref name="amount"/> to the user's available Balance.</summary>
    Task TopUpAsync(int userId, decimal amount);

    /// <summary>
    /// Debits <paramref name="amount"/> from the user's Balance and creates a
    /// Withdrawal record with <see cref="WithdrawalStatus.Requested"/>.
    /// Returns <c>false</c> if the balance is insufficient.
    /// </summary>
    Task<bool> RequestWithdrawalAsync(
        int userId, decimal amount, WithdrawalMethod method, string? reference);

    /// <summary>
    /// Transfers <paramref name="amount"/> directly from buyer's available Balance
    /// to seller's Balance (wallet-to-wallet payment without escrow).
    /// Throws <see cref="InvalidOperationException"/> if buyer has insufficient funds.
    /// </summary>
    Task TransferWalletToWalletAsync(int orderId, int buyerId, int sellerId, decimal amount);
}