using BCrypt.Net;
using ThriftLoop.Data;
using ThriftLoop.DTOs.Auth;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;
using ThriftLoop.Services.Auth.Interface;

namespace ThriftLoop.Services.Auth.Implementation;

public class RiderAuthService : IRiderAuthService
{
    private readonly IRiderRepository _riderRepo;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<RiderAuthService> _logger;

    public RiderAuthService(
        IRiderRepository riderRepo,
        ApplicationDbContext context,
        ILogger<RiderAuthService> logger)
    {
        _riderRepo = riderRepo;
        _context = context;
        _logger = logger;
    }

    public async Task<Rider?> RegisterAsync(RiderRegisterDTO dto)
    {
        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        if (await _riderRepo.EmailExistsAsync(normalizedEmail))
            return null;

        var rider = new Rider
        {
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            FullName = dto.FullName.Trim(),
            PhoneNumber = dto.PhoneNumber.Trim(),
            IsApproved = false,
            CreatedAt = DateTime.UtcNow
        };

        await _riderRepo.CreateAsync(rider);

        // Auto-provision an empty wallet for every new rider
        _context.Wallets.Add(new Wallet
        {
            RiderId = rider.Id,
            Balance = 0m,
            PendingBalance = 0m,
            UpdatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        _logger.LogInformation("New rider registered: {RiderId}.", rider.Id);
        return rider;
    }

    public async Task<Rider?> ValidateCredentialsAsync(LoginDTO dto)
    {
        var rider = await _riderRepo.GetByEmailAsync(dto.Email.Trim().ToLowerInvariant());

        if (rider is null || string.IsNullOrEmpty(rider.PasswordHash))
            return null;

        return BCrypt.Net.BCrypt.Verify(dto.Password, rider.PasswordHash) ? rider : null;
    }

    public async Task<Rider?> GetByIdAsync(int id)
        => await _riderRepo.GetByIdAsync(id);
}