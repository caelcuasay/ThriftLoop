using ThriftLoop.Enums;

namespace ThriftLoop.Models;

public class Transaction
{
    public int Id { get; set; }
    public int? OrderId { get; set; }
    public int FromUserId { get; set; }
    public int? ToUserId { get; set; }      // Made nullable
    public int? ToRiderId { get; set; }     // NEW - for rider payments
    public decimal Amount { get; set; }
    public TransactionType Type { get; set; }
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public Order? Order { get; set; }
    public User? FromUser { get; set; }
    public User? ToUser { get; set; }
    public Rider? ToRider { get; set; }     // NEW navigation
}