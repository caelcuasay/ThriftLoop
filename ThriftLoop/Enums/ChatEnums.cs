// Enums/ChatEnums.cs
namespace ThriftLoop.Enums;

/// <summary>
/// Delivery status of a chat message.
/// </summary>
public enum MessageStatus
{
    /// <summary>
    /// Message has been saved to the database but not yet delivered to the recipient.
    /// </summary>
    Sent = 1,

    /// <summary>
    /// Message has been delivered to the recipient (they received it via SignalR
    /// or loaded the conversation).
    /// </summary>
    Delivered = 2,

    /// <summary>
    /// Message has been seen/read by the recipient (scrolled into view or explicitly marked).
    /// </summary>
    Read = 3
}

/// <summary>
/// Type of chat message for rendering different bubble styles.
/// </summary>
public enum MessageType
{
    /// <summary>
    /// Standard text message sent by a user.
    /// </summary>
    Text = 1,

    /// <summary>
    /// Embedded order reference card (item details, price, condition).
    /// Sent when a buyer contacts a seller about an item or when an order is created.
    /// </summary>
    OrderReference = 2,

    /// <summary>
    /// Meeting proposal message (e.g., "Let's meet at SM North at 3pm").
    /// </summary>
    MeetingProposal = 3,

    /// <summary>
    /// System-generated message confirming an order was placed.
    /// </summary>
    OrderConfirmed = 4,

    /// <summary>
    /// System reminder about pending payment or action needed.
    /// </summary>
    PaymentReminder = 5,

    /// <summary>
    /// Context detail card for item transactions.
    /// </summary>
    ContextCard = 6
}