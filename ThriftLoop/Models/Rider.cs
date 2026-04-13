using System;
using System.Collections.Generic;

namespace ThriftLoop.Models
{
    public class Rider
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? PasswordHash { get; set; }
        public bool IsApproved { get; set; } = false;
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Application fields
        public string? DriversLicense { get; set; }
        public string? VehicleType { get; set; }
        public string? VehicleColor { get; set; }
        public string? LicensePlate { get; set; }
        public string? Address { get; set; }

        // Optional geolocation coordinates for the address
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }

        // Rejection tracking
        public string? RejectionReason { get; set; }
        public DateTime? RejectedAt { get; set; }
        public DateTime? ResubmittedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public int? ActiveDeliveryId { get; set; }
        public DateTime? ActiveDeliveryStartedAt { get; set; }

        public Wallet? Wallet { get; set; }
        public ICollection<Delivery> Deliveries { get; set; } = new List<Delivery>();
        public Delivery? ActiveDelivery { get; set; }
    }
}