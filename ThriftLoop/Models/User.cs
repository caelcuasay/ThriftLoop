namespace ThriftLoop.Models;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Null for users who registered exclusively via an external provider (e.g. Google).
    /// </summary>
    public string? PasswordHash { get; set; }

    /// <summary>
    /// Cryptographically secure reset token. Stored as plain text — cleared immediately
    /// after use or expiry. For higher-security requirements, store the SHA-256 hash instead.
    /// </summary>
    public string? PasswordResetToken { get; set; }

    /// <summary>
    /// UTC expiry for the reset token. Tokens are valid for 1 hour.
    /// </summary>
    public DateTime? PasswordResetTokenExpiry { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}