// Services/UserProfile/Implementation/UserProfileService.cs
using ThriftLoop.DTOs.User;
using ThriftLoop.Repositories.Interface;
using ThriftLoop.Services.UserProfile.Interface;

namespace ThriftLoop.Services.UserProfile.Implementation;

public class UserProfileService : IUserProfileService
{
    private readonly IUserRepository _userRepo;
    private readonly ILogger<UserProfileService> _logger;

    public UserProfileService(IUserRepository userRepo, ILogger<UserProfileService> logger)
    {
        _userRepo = userRepo;
        _logger   = logger;
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
            Id            = user.Id,
            Email         = user.Email,
            FullName      = user.FullName,
            PhoneNumber   = user.PhoneNumber,
            Address       = user.Address,
            Role          = user.Role.ToString(),
            CreatedAt     = user.CreatedAt,
            IsGoogleAccount = string.IsNullOrEmpty(user.PasswordHash)
        };
    }

    // ─────────────────────────────────────────
    //  UPDATE PROFILE
    // ─────────────────────────────────────────

    public async Task<bool> UpdateProfileAsync(int userId, UpdateProfileDTO dto)
    {
        // GetByIdAsync uses AsNoTracking — we get a detached entity that we can
        // mutate and hand to UpdateAsync, which re-attaches it as Modified.
        var user = await _userRepo.GetByIdAsync(userId);

        if (user is null)
            return false;

        user.FullName    = dto.FullName?.Trim();
        user.PhoneNumber = dto.PhoneNumber?.Trim();
        user.Address     = dto.Address?.Trim();

        await _userRepo.UpdateAsync(user);

        _logger.LogInformation("User {UserId} updated their profile.", userId);
        return true;
    }
}