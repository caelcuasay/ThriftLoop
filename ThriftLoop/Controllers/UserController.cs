// Controllers/UserController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ThriftLoop.DTOs.User;
using ThriftLoop.Enums;
using ThriftLoop.Services.UserProfile.Interface;

namespace ThriftLoop.Controllers;

/// <summary>
/// Handles the authenticated user's own profile and seller-application flow.
/// All routes require a logged-in, non-rider account.
/// Views live under Views/User/.
/// </summary>
[Authorize]
public class UserController : Controller
{
    private readonly IUserProfileService _profileService;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<UserController> _logger;

    public UserController(
        IUserProfileService profileService,
        IWebHostEnvironment env,
        ILogger<UserController> logger)
    {
        _profileService = profileService;
        _env = env;
        _logger = logger;
    }

    // ─────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────

    /// <summary>
    /// Reads the NameIdentifier claim set during SignInUserAsync.
    /// Returns null if the claim is missing or malformed.
    /// </summary>
    private int? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var id) ? id : null;
    }

    /// <summary>Bounces riders away — they use the Rider controller instead.</summary>
    private bool IsRider() =>
        User.HasClaim(c => c.Type == "IsRider" && c.Value == "true");

    // ─────────────────────────────────────────
    //  INDEX — profile view
    //  GET /User/Index  (or just /User)
    // ─────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (IsRider())
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

        var editForm = new UpdateProfileDTO
        {
            FullName = profile.FullName,
            PhoneNumber = profile.PhoneNumber,
            Address = profile.Address
        };

        ViewBag.Profile = profile;
        return View(editForm);
    }

    // ─────────────────────────────────────────
    //  UPDATE PROFILE
    //  POST /User/UpdateProfile
    // ─────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(UpdateProfileDTO dto)
    {
        if (IsRider())
            return RedirectToAction("Index", "Rider");

        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        if (!ModelState.IsValid)
        {
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

    // ─────────────────────────────────────────
    //  REQUESTS — seller application page
    //  GET /User/Requests
    // ─────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Requests()
    {
        if (IsRider())
            return RedirectToAction("Index", "Rider");

        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        // If the user is already a Seller they don't need this page
        if (User.IsInRole("Seller"))
        {
            TempData["InfoMessage"] = "You are already a seller.";
            return RedirectToAction(nameof(Index));
        }

        // Pass the current application status (may be null if never applied)
        var application = await _profileService.GetSellerApplicationAsync(userId.Value);
        ViewBag.Application = application;

        // Always provide a fresh empty form so Razor can render it
        return View(new SellerApplicationDTO());
    }

    // ─────────────────────────────────────────
    //  SUBMIT SELLER APPLICATION
    //  POST /User/SubmitSellerApplication
    // ─────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitSellerApplication(SellerApplicationDTO dto)
    {
        if (IsRider())
            return RedirectToAction("Index", "Rider");

        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        if (!ModelState.IsValid)
        {
            var application = await _profileService.GetSellerApplicationAsync(userId.Value);
            ViewBag.Application = application;
            return View("Requests", dto);
        }

        var uploadsPath = Path.Combine(_env.WebRootPath, "uploads");

        var result = await _profileService.SubmitSellerApplicationAsync(
            userId.Value, dto, uploadsPath);

        switch (result)
        {
            case SellerApplicationResult.Success:
                TempData["SuccessMessage"] =
                    "Your seller application has been submitted! " +
                    "We'll notify you once an admin reviews it.";
                return RedirectToAction(nameof(Requests));

            case SellerApplicationResult.AlreadySeller:
                TempData["InfoMessage"] = "Your account is already a seller.";
                return RedirectToAction(nameof(Index));

            case SellerApplicationResult.AlreadyApplied:
                TempData["ErrorMessage"] =
                    "You already have a pending or approved application. " +
                    "Please wait for admin review.";
                return RedirectToAction(nameof(Requests));

            default:
                // UserNotFound or unexpected — treat as a server error
                _logger.LogError(
                    "SubmitSellerApplication returned {Result} for User {UserId}.",
                    result, userId);
                ModelState.AddModelError(string.Empty,
                    "Something went wrong. Please try again.");
                var app = await _profileService.GetSellerApplicationAsync(userId.Value);
                ViewBag.Application = app;
                return View("Requests", dto);
        }
    }
}