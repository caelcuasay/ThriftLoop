using System;

namespace ThriftLoop.Models;

public class Rider
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public bool IsApproved { get; set; } = false;
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The ID of the delivery this rider is currently handling.
    /// Null if the rider has no active delivery.
    /// Used to enforce one-at-a-time rule.
    /// </summary>
    public int? ActiveDeliveryId { get; set; }

    /// <summary>UTC timestamp when the rider started their current active delivery.</summary>
    public DateTime? ActiveDeliveryStartedAt { get; set; }

    // Navigation
    public Wallet? Wallet { get; set; }

    /// <summary>Deliveries assigned to this rider (all historical).</summary>
    public ICollection<Delivery> Deliveries { get; set; } = new List<Delivery>();

    /// <summary>The currently active delivery, if any.</summary>
    public Delivery? ActiveDelivery { get; set; }
}