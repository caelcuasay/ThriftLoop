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
        Directory.CreateDirectory(uploadsDir);

        var ext = Path.GetExtension(dto.DriversLicense.FileName).ToLowerInvariant();
        var fileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadsDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
            await dto.DriversLicense.CopyToAsync(stream);

        var licenseRelativePath = $"/uploads/rider-licenses/{fileName}";

        var rider = new Rider
        {
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            FullName = dto.FullName.Trim(),
            PhoneNumber = dto.PhoneNumber.Trim(),
            IsApproved = false,
            CreatedAt = DateTime.UtcNow,
            DriversLicense = licenseRelativePath,
            VehicleType = dto.VehicleType.Trim(),
            VehicleColor = dto.VehicleColor.Trim(),
            LicensePlate = dto.LicensePlate.Trim().ToUpperInvariant(),
            Address = dto.Address.Trim(),
        };

        await _riderRepo.CreateAsync(rider);

        // REMOVED: Wallet creation - wallet will be created when admin approves the rider
        // REMOVED: Transaction creation for wallet seeding

        await _context.SaveChangesAsync();

        _logger.LogInformation("New rider registration submitted: {RiderId} (pending approval)", rider.Id);

        return rider;
    }

    public async Task<Rider?> GetRejectedApplicationAsync(int id)
    {
        var rider = await _riderRepo.GetByIdAsync(id);
        if (rider != null && !rider.IsApproved && !string.IsNullOrEmpty(rider.RejectionReason))
        {
            return rider;
        }
        return null;
    }

    // Services/Implementation/RiderAuthService.cs

    public async Task<bool> UpdateApplicationAsync(RiderEditDTO dto)
    {
        try
        {
            var rider = await _riderRepo.GetByIdAsync(dto.Id);
            if (rider == null || rider.IsApproved)
                return false;

            // Update basic info
            rider.FullName = dto.FullName.Trim();
            rider.Email = dto.Email.Trim().ToLowerInvariant();
            rider.PhoneNumber = dto.PhoneNumber.Trim();
            rider.Address = dto.Address.Trim();
            rider.VehicleType = dto.VehicleType.Trim();
            rider.VehicleColor = dto.VehicleColor.Trim();
            rider.LicensePlate = dto.LicensePlate.Trim().ToUpperInvariant();

            // Update password if provided
            if (!string.IsNullOrWhiteSpace(dto.NewPassword))
            {
                rider.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            }

            // Update license photo if provided
            if (dto.DriversLicense != null)
            {
                // Delete old file if exists
                if (!string.IsNullOrEmpty(rider.DriversLicense))
                {
                    var oldPath = Path.Combine(_env.WebRootPath, rider.DriversLicense.TrimStart('/'));
                    if (File.Exists(oldPath))
                        File.Delete(oldPath);
                }

                var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "rider-licenses");
                Directory.CreateDirectory(uploadsDir);

                var ext = Path.GetExtension(dto.DriversLicense.FileName).ToLowerInvariant();
                var fileName = $"{Guid.NewGuid()}{ext}";
                var filePath = Path.Combine(uploadsDir, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                    await dto.DriversLicense.CopyToAsync(stream);

                rider.DriversLicense = $"/uploads/rider-licenses/{fileName}";
            }

            // Mark as resubmitted
            rider.ResubmittedAt = DateTime.UtcNow;  // Set resubmission timestamp
            rider.UpdatedAt = DateTime.UtcNow;
            // Keep RejectionReason and RejectedAt for history

            await _riderRepo.UpdateAsync(rider);

            _logger.LogInformation("Rider {RiderId} resubmitted their application after rejection.", rider.Id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating rider application for ID: {Id}", dto.Id);
            return false;
        }
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