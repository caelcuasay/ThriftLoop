// DTOs/Chat/StartConversationDTO.cs
using System.ComponentModel.DataAnnotations;

namespace ThriftLoop.DTOs.Chat;

/// <summary>
/// Request DTO for explicitly starting a new conversation with a user.
/// </summary>
public class StartConversationDTO
{
    [Required(ErrorMessage = "Recipient ID is required.")]
    public int RecipientId { get; set; }

    /// <summary>
    /// Optional initial message to send when starting the conversation.
    /// </summary>
    [StringLength(2000, ErrorMessage = "Message cannot exceed 2000 characters.")]
    public string? InitialMessage { get; set; }
}