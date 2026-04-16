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