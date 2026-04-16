// DTOs/Chat/SendMessageDTO.cs
using System.ComponentModel.DataAnnotations;
using ThriftLoop.Enums;

namespace ThriftLoop.DTOs.Chat;

/// <summary>
/// Request DTO for sending a new message.
/// </summary>
public class SendMessageDTO
{
    /// <summary>
    /// ID of the conversation to send the message to.
    /// Required if not starting a new conversation.
    /// </summary>
    public int? ConversationId { get; set; }

    /// <summary>
    /// ID of the recipient user.
    /// Required only when starting a new conversation (ConversationId is null).
    /// </summary>
    public int? RecipientId { get; set; }

    [Required(ErrorMessage = "Message content is required.")]
    [StringLength(2000, ErrorMessage = "Message cannot exceed 2000 characters.")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Type of message being sent. Defaults to Text.
    /// </summary>
    public MessageType MessageType { get; set; } = MessageType.Text;

    /// <summary>
    /// Optional item ID. When provided, this message is an inquiry about a specific item.
    /// Used when a buyer contacts a seller from the item details page.
    /// </summary>
    public int? ContextItemId { get; set; }

    /// <summary>
    /// Optional order ID. When provided, this message references a confirmed order.
    /// </summary>
    public int? ContextOrderId { get; set; }

    /// <summary>
    /// Additional metadata for rich message types (JSON string).
    /// Example: {"location":"SM North EDSA","proposedTime":"2026-04-20T15:00:00Z"}
    /// </summary>
    public string? MetadataJson { get; set; }
}