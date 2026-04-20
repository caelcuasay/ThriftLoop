// Models/ContextCard.cs
using System.ComponentModel.DataAnnotations;
using ThriftLoop.Enums;
using ThriftLoop.DTOs.Chat;

namespace ThriftLoop.Models;

/// <summary>
/// Represents a context detail card for item transactions in chat.
/// Tracks the state and actions available for each item inquiry.
/// </summary>
public class ContextCard
{
    public int Id { get; set; }

    /// <summary>
    /// The conversation this context card belongs to.
    /// </summary>
    public int ConversationId { get; set; }

    /// <summary>
    /// The item being transacted.
    /// </summary>
    public int ItemId { get; set; }

    /// <summary>
    /// The user who is selling the item.
    /// </summary>
    public int SellerId { get; set; }

    /// <summary>
    /// The user who is buying the item.
    /// </summary>
    public int BuyerId { get; set; }

    /// <summary>
    /// The order created when the transaction is accepted (if any).
    /// </summary>
    public int? OrderId { get; set; }

    /// <summary>
    /// Current status of the context card transaction.
    /// </summary>
    public ContextCardStatus Status { get; set; } = ContextCardStatus.Pending;

    /// <summary>
    /// UTC timestamp when the context card was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// UTC timestamp when the context card expires (1 hour after creation if not accepted).
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// UTC timestamp when the transaction was completed (if applicable).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Payment method selected by buyer (after item received).
    /// </summary>
    public ThriftLoop.Enums.PaymentMethod? PaymentMethod { get; set; }

    /// <summary>
    /// Additional metadata stored as JSON for the context card.
    /// </summary>
    [MaxLength(1000)]
    public string? MetadataJson { get; set; }

    // Navigation Properties
    public Conversation Conversation { get; set; } = null!;
    public Item Item { get; set; } = null!;
    public User Seller { get; set; } = null!;
    public User Buyer { get; set; } = null!;
    public Order? Order { get; set; }

    /// <summary>
    /// Helper property to check if the card is expired.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow > ExpiresAt && Status != ContextCardStatus.Completed;

    /// <summary>
    /// Helper property to check if the card is active (not cancelled, declined, or expired).
    /// </summary>
    public bool IsActive => Status == ContextCardStatus.Pending || 
                           Status == ContextCardStatus.Accepted || 
                           Status == ContextCardStatus.ItemHandedOff || 
                           Status == ContextCardStatus.ItemReceived;
}
