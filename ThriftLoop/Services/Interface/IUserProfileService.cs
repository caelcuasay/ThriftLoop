// Services/UserProfile/Interface/IUserProfileService.cs
using ThriftLoop.DTOs.User;

namespace ThriftLoop.Services.UserProfile.Interface;

public interface IUserProfileService
{
    /// <summary>
    /// Returns a read-only profile snapshot for the given user ID.
    /// Returns null when the user does not exist.
    /// </summary>
    Task<UserProfileDTO?> GetProfileAsync(int userId);

    /// <summary>
    /// Applies FullName, PhoneNumber, and Address from the DTO.
    /// Returns false when the user does not exist.
    /// </summary>
    Task<bool> UpdateProfileAsync(int userId, UpdateProfileDTO dto);
}