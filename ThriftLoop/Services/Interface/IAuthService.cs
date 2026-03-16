using ThriftLoop.DTOs.Auth;
using ThriftLoop.Models;

namespace ThriftLoop.Services.Auth.Interface;

public interface IAuthService
{
    /// <summary>
    /// Registers a new user. Returns the created User on success,
    /// or null if the email is already taken.
    /// </summary>
    Task<User?> RegisterAsync(RegisterDTO dto);

    /// <summary>
    /// Validates credentials. Returns the matching User on success,
    /// or null if invalid.
    /// </summary>
    Task<User?> ValidateCredentialsAsync(LoginDTO dto);

    /// <summary>
    /// Looks up an existing user by email, or creates a new password-less
    /// account for first-time Google sign-in.
    /// </summary>
    Task<User> FindOrCreateGoogleUserAsync(string email);

    /// <summary>Hashes a plain-text password using BCrypt.</summary>
    string HashPassword(string plainTextPassword);

    /// <summary>Verifies a plain-text password against a stored BCrypt hash.</summary>
    bool VerifyPassword(string plainTextPassword, string passwordHash);
}