namespace ThriftLoop.Enums;

/// <summary>
/// Describes what a transaction represents in the money lifecycle.
/// </summary>
public enum TransactionType
{
    /// <summary>
    /// Funds moved from buyer Balance → buyer PendingBalance when an order is
    /// confirmed. Money is "in escrow" until the buyer marks delivery.
    /// </summary>
    EscrowHold,

    /// <summary>
    /// Funds moved from buyer PendingBalance → seller Balance when the buyer
    /// marks the order as delivered / completed.
    /// </summary>
    EscrowRelease,

    /// <summary>Seller withdraws available Balance to their bank or pickup point.</summary>
    Withdrawal,

    /// <summary>
    /// Cash-on-delivery funds collected by a rider and credited to the seller's
    /// wallet. Mirrors EscrowRelease but for COD orders.
    /// </summary>
    CashCollection,

    /// <summary>Funds added to a wallet (demo top-up or future payment gateway).</summary>
    TopUp,

    DeliveryFeePayment,

    /// <summary>
    /// Direct wallet-to-wallet payment without escrow.
    /// Funds moved from buyer Balance → seller Balance.
    /// </summary>
    WalletPayment

}

/// <summary>
/// Lifecycle state of a single transaction record.
/// </summary>
public enum TransactionStatus
{
    /// <summary>Initiated but not yet settled.</summary>
    Pending,

    /// <summary>Successfully settled — balances have been updated.</summary>
    Completed,

    /// <summary>Failed during processing — balances were not changed.</summary>
    Failed
}