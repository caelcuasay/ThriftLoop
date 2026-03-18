using ThriftLoop.DTOs.Auth;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;
using ThriftLoop.Services.Auth.Interface;
using ThriftLoop.Services.WalletManagement.Interface;

namespace ThriftLoop.Services.Auth.Implementation;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IWalletService _walletService;
    private readonly ILogger<AuthService> _logger;

    private const int BcryptWorkFactor = 12;

    public AuthService(
        IUserRepository userRepository,
        IWalletService walletService,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _walletService = walletService;
        _logger = logger;
    }

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

        // Auto-create wallet with ₱1,000 demo seed balance.
        await _walletService.GetOrCreateWalletAsync(user.Id);

        _logger.LogInformation("New user registered with ID {UserId}. Wallet seeded.", user.Id);
        return user;
    }

    public async Task<User?> ValidateCredentialsAsync(LoginDTO dto)
    {
        var user = await _userRepository.GetByEmailAsync(dto.Email.Trim().ToLowerInvariant());

        if (user is null)
        {
            BCrypt.Net.BCrypt.Verify(dto.Password, BCrypt.Net.BCrypt.HashPassword("dummy"));
            return null;
        }

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

    public async Task<User> FindOrCreateGoogleUserAsync(string email)
    {
        var normalised = email.Trim().ToLowerInvariant();

        var existing = await _userRepository.GetByEmailAsync(normalised);
        if (existing is not null)
        {
            // Ensure wallet exists for users who registered before wallet feature.
            await _walletService.GetOrCreateWalletAsync(existing.Id);
            _logger.LogInformation("Google sign-in: returning existing user {UserId}.", existing.Id);
            return existing;
        }

        var newUser = new User
        {
            Email = normalised,
            PasswordHash = null,
            CreatedAt = DateTime.UtcNow
        };

        newUser.Id = await _userRepository.CreateAsync(newUser);

        await _walletService.GetOrCreateWalletAsync(newUser.Id);

        _logger.LogInformation(
            "Google sign-in: created new user {UserId} for '{Email}'. Wallet seeded.",
            newUser.Id, normalised);

        return newUser;
    }

    public string HashPassword(string plainTextPassword)
        => BCrypt.Net.BCrypt.HashPassword(plainTextPassword, BcryptWorkFactor);

    public bool VerifyPassword(string plainTextPassword, string passwordHash)
        => BCrypt.Net.BCrypt.Verify(plainTextPassword, passwordHash);
}