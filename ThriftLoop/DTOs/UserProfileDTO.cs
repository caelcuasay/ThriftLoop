// DTOs/User/UserProfileDTO.cs
namespace ThriftLoop.DTOs.User;

/// <summary>
/// Read-only snapshot shown on the profile page.
/// </summary>
public class UserProfileDTO
{
    public int Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? FullName { get; init; }
    public string? PhoneNumber { get; init; }
    public string? Address { get; init; }
    public string Role { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }

    /// <summary>True when the account was created via Google and has no password.</summary>
    public bool IsGoogleAccount { get; init; }
}