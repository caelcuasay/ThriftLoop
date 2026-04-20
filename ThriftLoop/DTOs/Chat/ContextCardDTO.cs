// DTOs/Chat/ContextCardDTO.cs
using ThriftLoop.Enums;

namespace ThriftLoop.DTOs.Chat;

/// <summary>
/// Represents a context detail card for item transactions in chat.
/// </summary>
public class ContextCardDTO
{
    public int Id { get; set; }
    
    public int ConversationId { get; set; }
    
    public int ItemId { get; set; }
    
    public string ItemTitle { get; set; } = string.Empty;
    
    public decimal ItemPrice { get; set; }
    
    public string? ItemImageUrl { get; set; }
    
    public string Condition { get; set; } = string.Empty;
    
    public string? Size { get; set; }
    
    public string Category { get; set; } = string.Empty;
    
    public int SellerId { get; set; }
    
    public string SellerName { get; set; } = string.Empty;
    
    public int BuyerId { get; set; }
    
    public string BuyerName { get; set; } = string.Empty;
    
    public int? OrderId { get; set; }
    
    /// <summary>
    /// Current status of the context card transaction.
    /// </summary>
    public ContextCardStatus Status { get; set; }
    
    /// <summary>
    /// When the context card was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// When the context card expires (1 hour after creation if not accepted).
    /// </summary>
    public DateTime ExpiresAt { get; set; }
    
    /// <summary>
    /// When the transaction was completed (if applicable).
    /// </summary>
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>
    /// Payment method selected by buyer (after item received).
    /// </summary>
    public PaymentMethod? PaymentMethod { get; set; }
    
    /// <summary>
    /// Whether the current user is the seller in this transaction.
    /// </summary>
    public bool IsCurrentUserSeller { get; set; }
    
    /// <summary>
    /// Whether the current user is the buyer in this transaction.
    /// </summary>
    public bool IsCurrentUserBuyer { get; set; }
    
    /// <summary>
    /// Available actions for the current user based on their role and card status.
    /// </summary>
    public List<ContextCardAction> AvailableActions { get; set; } = new();
    
    /// <summary>
    /// Whether the card is expired.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow > ExpiresAt && Status != ContextCardStatus.Completed;
    
    public bool IsActive => Status == ContextCardStatus.Pending || 
                           Status == ContextCardStatus.Accepted || 
                           Status == ContextCardStatus.ItemHandedOff || 
                           Status == ContextCardStatus.ItemReceived;
    
    /// <summary>
    /// Time remaining until expiration (formatted string).
    /// </summary>
    public string TimeRemaining
    {
        get
        {
            if (Status == ContextCardStatus.Completed)
                return "Completed";
                
            var remaining = ExpiresAt - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                return "Expired";
                
            if (remaining.TotalHours >= 1)
                return $"{remaining.Hours}h {remaining.Minutes}m";
                
            return $"{remaining.Minutes}m {remaining.Seconds}s";
        }
    }
}

/// <summary>
/// Status of a context card transaction.
/// </summary>
public enum ContextCardStatus
{
    /// <summary>
    /// Initial state - buyer has inquired, seller hasn't responded yet.
    /// </summary>
    Pending = 1,
    
    /// <summary>
    /// Seller has accepted the transaction.
    /// </summary>
    Accepted = 2,
    
    /// <summary>
    /// Seller has marked the item as handed off.
    /// </summary>
    ItemHandedOff = 3,
    
    /// <summary>
    /// Buyer has marked the item as received.
    /// </summary>
    ItemReceived = 4,
    
    /// <summary>
    /// Transaction is completed (payment selected/finalized).
    /// </summary>
    Completed = 5,
    
    /// <summary>
    /// Transaction was cancelled by buyer.
    /// </summary>
    Cancelled = 6,
    
    /// <summary>
    /// Transaction was declined by seller.
    /// </summary>
    Declined = 7,
    
    /// <summary>
    /// Transaction expired (1 hour without acceptance).
    /// </summary>
    Expired = 8
}

/// <summary>
/// Available actions for a context card.
/// </summary>
public enum ContextCardAction
{
    /// <summary>
    /// Seller accepts the transaction.
    /// </summary>
    Accept = 1,
    
    /// <summary>
    /// Seller declines the transaction.
    /// </summary>
    Decline = 2,
    
    /// <summary>
    /// Buyer cancels the inquiry.
    /// </summary>
    Cancel = 3,
    
    /// <summary>
    /// Seller marks item as handed off.
    /// </summary>
    ItemHandedOff = 4,
    
    /// <summary>
    /// Buyer marks item as received.
    /// </summary>
    ItemReceived = 5,
    
    /// <summary>
    /// Buyer selects payment method.
    /// </summary>
    SelectPayment = 6
}

/// <summary>
/// Payment methods available after item is received.
/// </summary>
public enum PaymentMethod
{
    /// <summary>
    /// Use in-system wallet.
    /// </summary>
    Wallet = 1,
    
    /// <summary>
    /// Cash on hand/delivery.
    /// </summary>
    Cash = 2
}
