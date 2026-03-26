using ThriftLoop.Enums;

namespace ThriftLoop.Models;

/// <summary>
/// Represents a delivery job assigned to a rider for a specific order.
/// Tracks the physical delivery lifecycle separate from order payment state.
/// </summary>
public class Delivery
{
    public int Id { get; set; }

    // ── Foreign keys ──────────────────────────────────────────────────────────

    /// <summary>FK → Orders.Id — the order being delivered.</summary>
    public int OrderId { get; set; }

    /// <summary>FK → Riders.Id — the rider assigned to this delivery (null until accepted).</summary>
    public int? RiderId { get; set; }

    // ── Delivery data ─────────────────────────────────────────────────────────

    /// <summary>Current state of this delivery.</summary>
    public DeliveryStatus Status { get; set; } = DeliveryStatus.Available;

    /// <summary>UTC timestamp when the delivery was created (at order confirmation).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp when a rider accepted the delivery.</summary>
    public DateTime? AcceptedAt { get; set; }

    /// <summary>UTC timestamp when the rider marked the item as picked up.</summary>
    public DateTime? PickedUpAt { get; set; }

    /// <summary>UTC timestamp when the rider marked the item as delivered.</summary>
    public DateTime? DeliveredAt { get; set; }

    /// <summary>UTC timestamp when the buyer confirmed receipt.</summary>
    public DateTime? ConfirmedByBuyerAt { get; set; }

    // ── Navigation properties ─────────────────────────────────────────────────

    public Order? Order { get; set; }
    public Rider? Rider { get; set; }
}