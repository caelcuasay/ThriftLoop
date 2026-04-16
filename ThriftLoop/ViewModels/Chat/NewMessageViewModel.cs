// ViewModels/Chat/NewMessageViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace ThriftLoop.ViewModels.Chat;

/// <summary>
/// ViewModel for the new message / start conversation form.
/// </summary>
public class NewMessageViewModel
{
    /// <summary>
    /// ID of the recipient user.
    /// </summary>
    [Required(ErrorMessage = "Please select a recipient.")]
    public int RecipientId { get; set; }

    /// <summary>
    /// Optional initial message to send.
    /// </summary>
    [StringLength(2000, ErrorMessage = "Message cannot exceed 2000 characters.")]
    public string? InitialMessage { get; set; }

    /// <summary>
    /// Display name of the selected recipient (for confirmation UI).
    /// </summary>
    public string? RecipientName { get; set; }

    /// <summary>
    /// Whether this is being initiated from a user's profile page.
    /// </summary>
    public bool FromProfile { get; set; }

    /// <summary>
    /// Return URL to redirect back to after starting the conversation.
    /// </summary>
    public string? ReturnUrl { get; set; }

    /// <summary>
    /// Maximum allowed message length.
    /// </summary>
    public int MaxMessageLength => 2000;

    /// <summary>
    /// Characters remaining for the initial message.
    /// </summary>
    public int RemainingCharacters => MaxMessageLength - (InitialMessage?.Length ?? 0);
}