// DTOs/Chat/InquiryActionDTO.cs
using System.ComponentModel.DataAnnotations;

namespace ThriftLoop.DTOs.Chat;

/// <summary>
/// DTO for accepting or declining an item inquiry.
/// </summary>
public class InquiryActionDTO
{
    /// <summary>
    /// The ID of the conversation containing the inquiry.
    /// </summary>
    [Required]
    public int ConversationId { get; set; }

    /// <summary>
    /// The ID of the order reference message being acted upon.
    /// </summary>
    [Required]
    public int MessageId { get; set; }

    /// <summary>
    /// Optional note from the seller (e.g., "Can meet tomorrow at 3pm").
    /// </summary>
    [StringLength(500)]
    public string? Note { get; set; }
}

/// <summary>
/// Response DTO after an inquiry action is taken.
/// </summary>
public class InquiryActionResponseDTO
{
    /// <summary>
    /// Whether the action was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The updated inquiry status.
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// User-facing message.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Error message if success is false.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// The updated order reference DTO to refresh the UI.
    /// </summary>
    public OrderReferenceDTO? UpdatedReference { get; set; }
}