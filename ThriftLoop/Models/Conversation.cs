// Models/Conversation.cs
using System.ComponentModel.DataAnnotations;

namespace ThriftLoop.Models;

/// <summary>
/// Represents a 1-on-1 direct messaging conversation between two users.
/// The pair (UserOneId, UserTwoId) is unique to prevent duplicate conversations.
/// </summary>
public class Conversation
{
    public int Id { get; set; }

    /// <summary>
    /// The user who initiated the conversation (lower ID by convention, but not enforced).
    /// </summary>
    public int UserOneId { get; set; }

    /// <summary>
    /// The other participant in the conversation.
    /// </summary>
    public int UserTwoId { get; set; }

    /// <summary>
    /// Timestamp when the conversation was created (first message sent).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp of the most recent message in this conversation.
    /// Used for sorting the inbox.
    /// </summary>
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

    // ── Navigation Properties ─────────────────────────────────────────────────

    public User UserOne { get; set; } = null!;
    public User UserTwo { get; set; } = null!;

    public ICollection<Message> Messages { get; set; } = new List<Message>();
}