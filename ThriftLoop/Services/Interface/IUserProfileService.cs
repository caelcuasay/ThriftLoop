// Services/UserProfile/Interface/IUserProfileService.cs
using ThriftLoop.DTOs.User;
using ThriftLoop.Enums;

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

    // ─────────────────────────────────────────
    //  SELLER APPLICATION
    // ─────────────────────────────────────────

    /// <summary>
    /// Returns the user's current seller application, or null if they have
    /// never submitted one.
    /// </summary>
    Task<SellerApplicationStatusDTO?> GetSellerApplicationAsync(int userId);

    /// <summary>
    /// Creates a new seller application for the given user.
    /// Saves the gov-ID file under <paramref name="uploadsRootPath"/>/gov-ids/.
    /// </summary>
    /// <param name="userId">The authenticated user's ID.</param>
    /// <param name="dto">Form data including the uploaded gov-ID file.</param>
    /// <param name="uploadsRootPath">
    ///   Absolute path to the wwwroot/uploads directory, typically
    ///   Path.Combine(env.WebRootPath, "uploads").
    /// </param>
    /// <returns>
    ///   <see cref="SellerApplicationResult.Success"/> on success;
    ///   one of the other values if the request cannot be fulfilled.
    /// </returns>
    Task<SellerApplicationResult> SubmitSellerApplicationAsync(
        int userId,
        SellerApplicationDTO dto,
        string uploadsRootPath);
}