using System;
using ThriftLoop.Enums;

namespace ThriftLoop.Models;

public class Wallet
{
    public int Id { get; set; }
    public decimal Balance { get; set; }
    public decimal PendingBalance { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Foreign keys - UserId and RiderId are mutually exclusive
    public int? UserId { get; set; }
    public int? RiderId { get; set; }

    // Navigation properties
    public User? User { get; set; }
    public Rider? Rider { get; set; }
}