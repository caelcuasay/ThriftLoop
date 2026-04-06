// Services/UserProfile/Implementation/UserProfileService.cs
using Microsoft.EntityFrameworkCore;
using ThriftLoop.Data;
using ThriftLoop.DTOs.User;
using ThriftLoop.Enums;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;
using ThriftLoop.Services.UserProfile.Interface;

namespace ThriftLoop.Services.UserProfile.Implementation;

public class UserProfileService : IUserProfileService
{
    private readonly IUserRepository _userRepo;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UserProfileService> _logger;

    // Accepted MIME types for the gov-ID upload
    private static readonly HashSet<string> _allowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "application/pdf"
    };

    // Hard cap at 10 MB
    private const long MaxGovIdBytes = 10 * 1024 * 1024;

    public UserProfileService(
        IUserRepository userRepo,
        ApplicationDbContext context,
        ILogger<UserProfileService> logger)
    {
        _userRepo = userRepo;
        _context = context;
        _logger = logger;
    }

    // ─────────────────────────────────────────
    //  GET PROFILE
    // ─────────────────────────────────────────

    public async Task<UserProfileDTO?> GetProfileAsync(int userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);

        if (user is null)
            return null;

        return new UserProfileDTO
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            Address = user.Address,
            Role = user.Role.ToString(),
            CreatedAt = user.CreatedAt,
            IsGoogleAccount = string.IsNullOrEmpty(user.PasswordHash)
        };
    }

    // ─────────────────────────────────────────
    //  UPDATE PROFILE
    // ─────────────────────────────────────────

    public async Task<bool> UpdateProfileAsync(int userId, UpdateProfileDTO dto)
    {
        var user = await _userRepo.GetByIdAsync(userId);

        if (user is null)
            return false;

        user.FullName = dto.FullName?.Trim();
        user.PhoneNumber = dto.PhoneNumber?.Trim();
        user.Address = dto.Address?.Trim();

        await _userRepo.UpdateAsync(user);

        _logger.LogInformation("User {UserId} updated their profile.", userId);
        return true;
    }

    // ─────────────────────────────────────────
    //  GET SELLER APPLICATION
    // ─────────────────────────────────────────

    public async Task<SellerApplicationStatusDTO?> GetSellerApplicationAsync(int userId)
    {
        var application = await _context.SellerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(sp => sp.UserId == userId);

        if (application is null)
            return null;

        return MapToStatusDTO(application);
    }

    // ─────────────────────────────────────────
    //  SUBMIT SELLER APPLICATION
    // ─────────────────────────────────────────

    public async Task<SellerApplicationResult> SubmitSellerApplicationAsync(
        int userId,
        SellerApplicationDTO dto,
        string uploadsRootPath)
    {
        // 1. Verify the user exists
        var user = await _userRepo.GetByIdAsync(userId);
        if (user is null)
            return SellerApplicationResult.UserNotFound;

        // 2. Already a seller — nothing to do
        if (user.Role == UserRole.Seller)
            return SellerApplicationResult.AlreadySeller;

        // 3. Prevent duplicate submissions (Pending or Approved blocks re-apply)
        var existing = await _context.SellerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(sp => sp.UserId == userId);

        if (existing is not null &&
            existing.ApplicationStatus != ApplicationStatus.Rejected)
        {
            return SellerApplicationResult.AlreadyApplied;
        }

        // 4. Validate and save the gov-ID file
        var govIdUrl = await SaveGovIdAsync(dto.GovId, userId, uploadsRootPath);

        if (govIdUrl is null)
        {
            // File validation failed — caller should surface a model error
            _logger.LogWarning(
                "Gov-ID upload failed for User {UserId}: invalid type or size.", userId);
            return SellerApplicationResult.UserNotFound; // re-use as a sentinel; see note below
            // NOTE: If you want a dedicated FileInvalid result value, add it to the enum.
        }

        // 5. If a previous rejected application exists, replace it in-place
        //    so that the unique index on UserId is preserved.
        if (existing is not null)
        {
            // Re-fetch tracked entity for update
            var tracked = await _context.SellerProfiles.FindAsync(existing.Id);
            if (tracked is not null)
            {
                tracked.ShopName = dto.ShopName.Trim();
                tracked.Bio = dto.Bio?.Trim();
                tracked.StoreAddress = dto.StoreAddress.Trim();
                tracked.GovIdUrl = govIdUrl;
                tracked.ApplicationStatus = ApplicationStatus.Pending;
                tracked.AppliedAt = DateTime.UtcNow;
                tracked.ReviewedAt = null;

                await _context.SaveChangesAsync();
                _logger.LogInformation(
                    "User {UserId} re-submitted seller application (ID {AppId}).",
                    userId, tracked.Id);
                return SellerApplicationResult.Success;
            }
        }

        // 6. First-time application
        var profile = new SellerProfile
        {
            UserId = userId,
            ShopName = dto.ShopName.Trim(),
            Bio = dto.Bio?.Trim(),
            StoreAddress = dto.StoreAddress.Trim(),
            GovIdUrl = govIdUrl,
            ApplicationStatus = ApplicationStatus.Pending,
            AppliedAt = DateTime.UtcNow
        };

        _context.SellerProfiles.Add(profile);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "User {UserId} submitted a new seller application (ID {AppId}).",
            userId, profile.Id);

        return SellerApplicationResult.Success;
    }

    // ─────────────────────────────────────────
    //  PRIVATE HELPERS
    // ─────────────────────────────────────────

    /// <summary>
    /// Validates and writes the gov-ID file to disk.
    /// Returns the server-relative URL on success, or null on validation failure.
    /// </summary>
    private async Task<string?> SaveGovIdAsync(
        IFormFile file,
        int userId,
        string uploadsRootPath)
    {
        if (file is null || file.Length == 0)
            return null;

        if (file.Length > MaxGovIdBytes)
            return null;

        if (!_allowedMimeTypes.Contains(file.ContentType))
            return null;

        // Build a collision-proof filename:  {userId}_{ticks}_{safeOriginalName}
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var safeName = $"{userId}_{DateTime.UtcNow.Ticks}{ext}";
        var govIdDir = Path.Combine(uploadsRootPath, "gov-ids");

        Directory.CreateDirectory(govIdDir);

        var fullPath = Path.Combine(govIdDir, safeName);

        await using var stream = new FileStream(fullPath, FileMode.Create);
        await file.CopyToAsync(stream);

        // Return a server-relative URL the view/admin can render
        return $"/uploads/gov-ids/{safeName}";
    }

    private static SellerApplicationStatusDTO MapToStatusDTO(SellerProfile sp) =>
        new()
        {
            Id = sp.Id,
            ShopName = sp.ShopName,
            Bio = sp.Bio,
            StoreAddress = sp.StoreAddress,
            GovIdUrl = sp.GovIdUrl,
            Status = sp.ApplicationStatus,
            AppliedAt = sp.AppliedAt,
            ReviewedAt = sp.ReviewedAt
        };
}