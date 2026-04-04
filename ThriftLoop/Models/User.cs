// Models/User.cs (UPDATED)
using ThriftLoop.Enums;

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

    // ── Role ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// The account's current role. Defaults to User on registration.
    /// Elevated to Seller only after an admin approves their SellerProfile application.
    /// Role is stamped into the auth cookie claims on login so controllers can gate
    /// access with a simple role check without hitting the database.
    /// </summary>
    public UserRole Role { get; set; } = UserRole.User;

    /// <summary>
    /// Whether the account has been disabled by an admin.
    /// Disabled users cannot log in or perform any actions.
    /// </summary>
    public bool IsDisabled { get; set; } = false;

    /// <summary>
    /// UTC timestamp when the account was disabled (if IsDisabled is true).
    /// </summary>
    public DateTime? DisabledAt { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────────

    /// <summary>
    /// Populated only if this user has submitted or been approved as a Seller.
    /// Null for regular Users and Riders. Use this to access shop branding data
    /// and application status without joining through Items.
    /// </summary>
    public SellerProfile? SellerProfile { get; set; }
}