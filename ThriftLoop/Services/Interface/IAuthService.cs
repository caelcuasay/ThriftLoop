using ThriftLoop.DTOs.Auth;
using ThriftLoop.Models;

namespace ThriftLoop.Services.Auth.Interface;

public interface IAuthService
{
    /// <summary>
    /// Creates a new user account. Returns null if the email is already taken.
    /// Also auto-provisions an empty Wallet for the new user.
    /// </summary>
    Task<User?> RegisterAsync(RegisterDTO dto);

    /// <summary>
    /// Verifies email + password. Returns the user on success, null on failure.
    /// </summary>
    Task<User?> ValidateCredentialsAsync(LoginDTO dto);

    /// <summary>
    /// Finds an existing user by email or creates a password-less account for
    /// users who sign in via Google for the first time.
    /// </summary>
    Task<User> FindOrCreateGoogleUserAsync(string email);

    /// <summary>
    /// Generates a reset token, persists it, and dispatches a reset email.
    /// Always returns true — never reveals whether the email exists.
    /// </summary>
    /// <param name="resetBaseUrl">
    /// The full URL to the ResetPassword action, e.g. "https://example.com/Auth/ResetPassword".
    /// Token + email are appended as query-string parameters.
    /// </param>
    Task<bool> ForgotPasswordAsync(ForgotPasswordDTO dto, string resetBaseUrl);

    /// <summary>
    /// Validates the reset token and updates the user's password.
    /// Returns false if the token is missing, expired, or does not match.
    /// </summary>
    Task<bool> ResetPasswordAsync(ResetPasswordDTO dto);
}