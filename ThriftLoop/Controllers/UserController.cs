// Controllers/UserController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ThriftLoop.DTOs.User;
//using ThriftLoop.Services.Interface;
using ThriftLoop.Services.UserProfile.Interface;

namespace ThriftLoop.Controllers;

/// <summary>
/// Handles the authenticated user's own profile.
/// All routes require a logged-in, non-rider account.
/// Views live under Views/User/.
/// </summary>
[Authorize]
public class UserController : Controller
{
    private readonly IUserProfileService _profileService;
    private readonly ILogger<UserController> _logger;

    public UserController(IUserProfileService profileService, ILogger<UserController> logger)
    {
        _profileService = profileService;
        _logger = logger;
    }

    // ─────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────

    /// <summary>
    /// Reads the NameIdentifier claim set during SignInUserAsync.
    /// Returns null and sets a 401 result if the claim is missing or malformed.
    /// </summary>
    private int? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var id) ? id : null;
    }

    // ─────────────────────────────────────────
    //  INDEX — profile view
    //  GET /User/Index  (or just /User)
    // ─────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // Riders have their own dashboard; bounce them out.
        if (User.HasClaim(c => c.Type == "IsRider" && c.Value == "true"))
            return RedirectToAction("Index", "Rider");

        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        var profile = await _profileService.GetProfileAsync(userId.Value);

        if (profile is null)
        {
            _logger.LogWarning("Profile not found for userId={UserId}.", userId);
            return NotFound();
        }

        // Pre-populate the edit form with existing values so the view can
        // show both the read-only snapshot and the editable form together.
        var editForm = new UpdateProfileDTO
        {
            FullName = profile.FullName,
            PhoneNumber = profile.PhoneNumber,
            Address = profile.Address
        };

        ViewBag.Profile = profile;         // read-only display data
        return View(editForm);             // model bound to the edit form
    }

    // ─────────────────────────────────────────
    //  UPDATE PROFILE
    //  POST /User/UpdateProfile
    // ─────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(UpdateProfileDTO dto)
    {
        if (User.HasClaim(c => c.Type == "IsRider" && c.Value == "true"))
            return RedirectToAction("Index", "Rider");

        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        if (!ModelState.IsValid)
        {
            // Re-fetch the read-only profile so the page header / email are correct.
            var profile = await _profileService.GetProfileAsync(userId.Value);
            ViewBag.Profile = profile;
            return View("Index", dto);
        }

        var success = await _profileService.UpdateProfileAsync(userId.Value, dto);

        if (!success)
        {
            _logger.LogWarning("UpdateProfile failed — user {UserId} not found.", userId);
            return NotFound();
        }

        TempData["ProfileSuccess"] = "Your profile has been updated.";
        return RedirectToAction(nameof(Index));
    }
}