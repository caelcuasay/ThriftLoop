using BCrypt.Net;
using ThriftLoop.Constants;  // ← ADD THIS
using ThriftLoop.Data;
using ThriftLoop.DTOs.Auth;
using ThriftLoop.Enums;       // ← ADD THIS for TransactionType
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;
using ThriftLoop.Services.Auth.Interface;

namespace ThriftLoop.Services.Auth.Implementation;

public class RiderAuthService : IRiderAuthService
{
    private readonly IRiderRepository _riderRepo;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<RiderAuthService> _logger;
    private readonly IWebHostEnvironment _env;
    public RiderAuthService(
        IRiderRepository riderRepo,
        ApplicationDbContext context,
        IWebHostEnvironment env,
        ILogger<RiderAuthService> logger)
    {
        _riderRepo = riderRepo;
        _context = context;
        _env = env;
        _logger = logger;
    }

    public async Task<Rider?> RegisterAsync(RiderRegisterDTO dto)
    {
        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        if (await _riderRepo.EmailExistsAsync(normalizedEmail))
            return null;

        // ── Save license image ──────────────────────────────────────
        var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "rider-licenses");
        Directory.CreateDirectory(uploadsDir); // no-op if already exists

        var ext = Path.GetExtension(dto.DriversLicense.FileName).ToLowerInvariant();
        var fileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadsDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
            await dto.DriversLicense.CopyToAsync(stream);

        var licenseRelativePath = $"/uploads/rider-licenses/{fileName}";
        // ───────────────────────────────────────────────────────────

        var rider = new Rider
        {
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            FullName = dto.FullName.Trim(),
            PhoneNumber = dto.PhoneNumber.Trim(),
            IsApproved = false,
            CreatedAt = DateTime.UtcNow,
            DriversLicense = licenseRelativePath,   // stores the file path
            VehicleType = dto.VehicleType.Trim(),
            VehicleColor = dto.VehicleColor.Trim(),
            LicensePlate = dto.LicensePlate.Trim().ToUpperInvariant(),
            Address = dto.Address.Trim(),
        };
                    

        await _riderRepo.CreateAsync(rider);

        // Auto-provision wallet with demo balance (using constant)
        var wallet = new Wallet
        {
            RiderId = rider.Id,
            Balance = WalletConstants.InitialBalance,   // ← FIXED: use constant
            PendingBalance = 0m,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Wallets.Add(wallet);

        // Audit the seed as a TopUp so the history is clean
        _context.Transactions.Add(new Transaction
        {
            OrderId = null,
            FromUserId = rider.Id,   // For riders, FromUserId = rider.Id (self)
            ToUserId = rider.Id,     // ToUserId = rider.Id (self)
            Amount = WalletConstants.InitialBalance,
            Type = TransactionType.TopUp,
            Status = TransactionStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "New rider registered: {RiderId} with ₱{SeedBalance} demo balance.",
            rider.Id, WalletConstants.InitialBalance);
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