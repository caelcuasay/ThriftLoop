// Areas/Mobile/Controllers/UserController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ThriftLoop.DTOs.User;
using ThriftLoop.Enums;
using ThriftLoop.Services.UserProfile.Interface;

namespace ThriftLoop.Areas.Mobile.Controllers;

[Area("Mobile")]
[Route("mobile/[controller]/[action]")]
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

    private int? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var id) ? id : null;
    }

    private bool IsRider() =>
        User.HasClaim(c => c.Type == "IsRider" && c.Value == "true");

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (IsRider())
            return RedirectToAction("Index", "Home", new { area = "Mobile" });

        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var profile = await _profileService.GetProfileAsync(userId.Value);
        if (profile is null) return NotFound();

        var editForm = new UpdateProfileDTO
        {
            FullName = profile.FullName,
            PhoneNumber = profile.PhoneNumber,
            Address = profile.Address,
            Latitude = profile.Latitude,
            Longitude = profile.Longitude
        };

        ViewBag.Profile = profile;
        ViewBag.SellerApplication = await _profileService.GetSellerApplicationAsync(userId.Value);
        return View(editForm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(UpdateProfileDTO dto)
    {
        if (IsRider())
            return RedirectToAction("Index", "Home", new { area = "Mobile" });

        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        if (!ModelState.IsValid)
        {
            var profile = await _profileService.GetProfileAsync(userId.Value);
            ViewBag.Profile = profile;
            return View("Index", dto);
        }

        var success = await _profileService.UpdateProfileAsync(userId.Value, dto);
        if (!success) return NotFound();

        TempData["ProfileSuccess"] = "Your profile has been updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Requests()
    {
        if (IsRider())
            return RedirectToAction("Index", "Home", new { area = "Mobile" });

        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        if (User.IsInRole("Seller"))
        {
            TempData["InfoMessage"] = "You are already a seller.";
            return RedirectToAction(nameof(Index));
        }

        var application = await _profileService.GetSellerApplicationAsync(userId.Value);
        ViewBag.Application = application;

        return View(new SellerApplicationDTO());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitSellerApplication(SellerApplicationDTO dto)
    {
        if (IsRider())
            return RedirectToAction("Index", "Home", new { area = "Mobile" });

        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        if (!ModelState.IsValid)
        {
            var application = await _profileService.GetSellerApplicationAsync(userId.Value);
            ViewBag.Application = application;
            return View("Requests", dto);
        }

        var uploadsPath = Path.Combine(_env.WebRootPath, "uploads");
        var result = await _profileService.SubmitSellerApplicationAsync(userId.Value, dto, uploadsPath);

        switch (result)
        {
            case SellerApplicationResult.Success:
                TempData["SuccessMessage"] = "Your seller application has been submitted!";
                return RedirectToAction(nameof(Requests));
            case SellerApplicationResult.AlreadySeller:
                TempData["InfoMessage"] = "Your account is already a seller.";
                return RedirectToAction(nameof(Index));
            case SellerApplicationResult.AlreadyApplied:
                TempData["ErrorMessage"] = "You already have a pending or approved application.";
                return RedirectToAction(nameof(Requests));
            default:
                var app = await _profileService.GetSellerApplicationAsync(userId.Value);
                ViewBag.Application = app;
                ModelState.AddModelError(string.Empty, "Something went wrong. Please try again.");
                return View("Requests", dto);
        }
    }
}