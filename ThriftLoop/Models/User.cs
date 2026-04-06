// Models/User.cs (UPDATED — added FullName, PhoneNumber, Address)
using ThriftLoop.Enums;

namespace ThriftLoop.Models;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;

    // ── Profile ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Optional display name set by the user. Distinct from Email / claims Name.
    /// </summary>
    public string? FullName { get; set; }

    /// <summary>
    /// E.164-style phone number, e.g. "+639171234567". Validated at the DTO layer.
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Free-text shipping / contact address supplied by the user.
    /// </summary>
    public string? Address { get; set; }

    // ── Security ──────────────────────────────────────────────────────────────

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
    /// </summary>
    public UserRole Role { get; set; } = UserRole.User;

    /// <summary>
    /// Whether the account has been disabled by an admin.
    /// </summary>
    public bool IsDisabled { get; set; } = false;

    /// <summary>
    /// UTC timestamp when the account was disabled (if IsDisabled is true).
    /// </summary>
    public DateTime? DisabledAt { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────────

    public SellerProfile? SellerProfile { get; set; }
}