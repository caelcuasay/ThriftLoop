namespace ThriftLoop.Enums;

/// <summary>
/// Lifecycle of an order from placement through resolution.
/// </summary>
public enum OrderStatus
{
    /// <summary>
    /// The buyer has confirmed on the checkout page but the seller has not yet
    /// acknowledged payment or arranged handover. This is the initial state.
    /// </summary>
    Pending,

    /// <summary>Payment and/or delivery have been confirmed by both parties.</summary>
    Completed,

    /// <summary>The order was cancelled before completion.</summary>
    Cancelled
}

/// <summary>
/// How the buyer intends to pay for this order.
/// </summary>
public enum PaymentMethod
{
    /// <summary>
    /// Funds are held in escrow from the buyer's ThriftLoop wallet and released
    /// to the seller when the buyer marks delivery.
    /// </summary>
    Wallet,

    /// <summary>
    /// Buyer pays cash to the rider on delivery. Funds are credited to the
    /// seller's wallet when a rider (or the buyer) marks cash as collected.
    /// </summary>
    Cash
}