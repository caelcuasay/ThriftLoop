// DTOs/Chat/SendMessageDTO.cs
using System.ComponentModel.DataAnnotations;

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
}