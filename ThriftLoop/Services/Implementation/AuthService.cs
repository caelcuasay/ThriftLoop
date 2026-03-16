using ThriftLoop.DTOs.Auth;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;
using ThriftLoop.Services.Auth.Interface;

namespace ThriftLoop.Services.Auth.Implementation;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<AuthService> _logger;

    private const int BcryptWorkFactor = 12;

    public AuthService(IUserRepository userRepository, ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    // ── Password registration ─────────────────────────────────────────────────

    public async Task<User?> RegisterAsync(RegisterDTO dto)
    {
        if (await _userRepository.EmailExistsAsync(dto.Email))
        {
            _logger.LogWarning("Registration failed: email '{Email}' is already in use.", dto.Email);
            return null;
        }

        var user = new User
        {
            Email = dto.Email.Trim().ToLowerInvariant(),
            PasswordHash = HashPassword(dto.Password),
            CreatedAt = DateTime.UtcNow
        };

        user.Id = await _userRepository.CreateAsync(user);

        _logger.LogInformation("New user registered with ID {UserId}.", user.Id);
        return user;
    }

    // ── Credential validation ─────────────────────────────────────────────────

    public async Task<User?> ValidateCredentialsAsync(LoginDTO dto)
    {
        var user = await _userRepository.GetByEmailAsync(dto.Email.Trim().ToLowerInvariant());

        if (user is null)
        {
            // Constant-time dummy verify to prevent timing attacks.
            BCrypt.Net.BCrypt.Verify(dto.Password, BCrypt.Net.BCrypt.HashPassword("dummy"));
            return null;
        }

        // A Google-only account has no password — refuse credential login.
        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            _logger.LogWarning(
                "Credential login attempted on Google-only account '{Email}'.", dto.Email);
            return null;
        }

        if (!VerifyPassword(dto.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt for email '{Email}'.", dto.Email);
            return null;
        }

        return user;
    }

    // ── Google OAuth ──────────────────────────────────────────────────────────

    public async Task<User> FindOrCreateGoogleUserAsync(string email)
    {
        var normalised = email.Trim().ToLowerInvariant();

        var existing = await _userRepository.GetByEmailAsync(normalised);
        if (existing is not null)
        {
            _logger.LogInformation(
                "Google sign-in: returning existing user {UserId}.", existing.Id);
            return existing;
        }

        // First-time Google sign-in — provision a password-less account.
        var newUser = new User
        {
            Email = normalised,
            PasswordHash = null,          // no password for external users
            CreatedAt = DateTime.UtcNow
        };

        newUser.Id = await _userRepository.CreateAsync(newUser);

        _logger.LogInformation(
            "Google sign-in: created new user {UserId} for '{Email}'.", newUser.Id, normalised);

        return newUser;
    }

    // ── Hashing helpers ───────────────────────────────────────────────────────

    public string HashPassword(string plainTextPassword)
        => BCrypt.Net.BCrypt.HashPassword(plainTextPassword, BcryptWorkFactor);

    public bool VerifyPassword(string plainTextPassword, string passwordHash)
        => BCrypt.Net.BCrypt.Verify(plainTextPassword, passwordHash);
}