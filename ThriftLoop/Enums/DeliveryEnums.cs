namespace ThriftLoop.Enums;

/// <summary>
/// Lifecycle of a delivery from listing to completion.
/// </summary>
public enum DeliveryStatus
{
    /// <summary>
    /// Initial state after order confirmation. No rider assigned yet.
    /// Visible to all riders in the job listings.
    /// </summary>
    Available,

    /// <summary>
    /// A rider has accepted the job and is en route to pickup.
    /// Rider is locked to this delivery and cannot accept others.
    /// </summary>
    Accepted,

    /// <summary>
    /// Rider has marked the item as picked up from the seller.
    /// Now en route to buyer.
    /// </summary>
    PickedUp,

    /// <summary>
    /// Rider has marked the item as delivered to the buyer.
    /// Awaiting buyer confirmation.
    /// </summary>
    Delivered,

    /// <summary>
    /// Buyer has confirmed receipt of the item.
    /// Terminal state — order payment should be completed if not already.
    /// </summary>
    Completed,

    /// <summary>
    /// Delivery was cancelled before completion.
    /// Rider becomes available again if they were assigned.
    /// </summary>
    Cancelled
}