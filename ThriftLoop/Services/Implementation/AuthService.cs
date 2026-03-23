using BCrypt.Net;
using ThriftLoop.Data;
using ThriftLoop.DTOs.Auth;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;
using ThriftLoop.Services.Auth.Interface;
using ThriftLoop.Services.Email.Interface;

namespace ThriftLoop.Services.Auth.Implementation;

/// <summary>
/// Requires NuGet: BCrypt.Net-Next (Install-Package BCrypt.Net-Next)
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepo;
    private readonly IEmailService _emailService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepo,
        IEmailService emailService,
        ApplicationDbContext context,
        ILogger<AuthService> logger)
    {
        _userRepo = userRepo;
        _emailService = emailService;
        _context = context;
        _logger = logger;
    }

    // ─────────────────────────────────────────
    //  REGISTER
    // ─────────────────────────────────────────

    public async Task<User?> RegisterAsync(RegisterDTO dto)
    {
        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        if (await _userRepo.EmailExistsAsync(normalizedEmail))
            return null;

        var user = new User
        {
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            CreatedAt = DateTime.UtcNow
        };

        await _userRepo.CreateAsync(user);

        // Auto-provision an empty wallet for every new user
        _context.Wallets.Add(new Wallet
        {
            UserId = user.Id,
            Balance = 0m,
            PendingBalance = 0m,
            UpdatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        _logger.LogInformation("New user registered: {UserId}.", user.Id);
        return user;
    }

    // ─────────────────────────────────────────
    //  LOGIN
    // ─────────────────────────────────────────

    public async Task<User?> ValidateCredentialsAsync(LoginDTO dto)
    {
        var user = await _userRepo.GetByEmailAsync(dto.Email.Trim().ToLowerInvariant());

        // Reject Google-only accounts — they have no password hash
        if (user is null || string.IsNullOrEmpty(user.PasswordHash))
            return null;

        return BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash) ? user : null;
    }

    // ─────────────────────────────────────────
    //  GOOGLE OAUTH
    // ─────────────────────────────────────────

    public async Task<User> FindOrCreateGoogleUserAsync(string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await _userRepo.GetByEmailAsync(normalizedEmail);

        if (user is not null)
            return user;

        // First-time Google sign-in: auto-provision a password-less account
        var newUser = new User
        {
            Email = normalizedEmail,
            PasswordHash = null,
            CreatedAt = DateTime.UtcNow
        };

        await _userRepo.CreateAsync(newUser);

        _context.Wallets.Add(new Wallet
        {
            UserId = newUser.Id,
            Balance = 0m,
            PendingBalance = 0m,
            UpdatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        _logger.LogInformation("Google user auto-provisioned: {UserId}.", newUser.Id);
        return newUser;
    }

    // ─────────────────────────────────────────
    //  FORGOT PASSWORD
    // ─────────────────────────────────────────

    public async Task<bool> ForgotPasswordAsync(ForgotPasswordDTO dto, string resetBaseUrl)
    {
        var user = await _userRepo.GetByEmailAsync(dto.Email.Trim().ToLowerInvariant());

        // Return true regardless — never leak whether the email is registered
        if (user is null)
            return true;

        // Generate a cryptographically secure 32-byte (64 hex char) token
        var token = Convert.ToHexString(
            System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

        user.PasswordResetToken = token;
        user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);

        await _userRepo.UpdateAsync(user);

        var resetLink = $"{resetBaseUrl}?token={Uri.EscapeDataString(token)}" +
                        $"&email={Uri.EscapeDataString(user.Email)}";

        await _emailService.SendPasswordResetEmailAsync(user.Email, resetLink);

        _logger.LogInformation("Password reset requested for user {UserId}.", user.Id);
        return true;
    }

    // ─────────────────────────────────────────
    //  RESET PASSWORD
    // ─────────────────────────────────────────

    public async Task<bool> ResetPasswordAsync(ResetPasswordDTO dto)
    {
        var user = await _userRepo.GetByEmailAsync(dto.Email.Trim().ToLowerInvariant());

        if (user is null
            || string.IsNullOrEmpty(user.PasswordResetToken)
            || user.PasswordResetTokenExpiry is null
            || user.PasswordResetToken != dto.Token
            || user.PasswordResetTokenExpiry < DateTime.UtcNow)
        {
            return false;
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiry = null;

        await _userRepo.UpdateAsync(user);

        _logger.LogInformation("Password reset completed for user {UserId}.", user.Id);
        return true;
    }
}