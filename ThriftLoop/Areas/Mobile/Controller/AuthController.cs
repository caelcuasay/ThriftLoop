// Areas/Mobile/Controllers/AuthController.cs
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ThriftLoop.DTOs.Auth;
using ThriftLoop.Models;
using ThriftLoop.Services.Auth.Interface;

namespace ThriftLoop.Areas.Mobile.Controllers;

[Area("Mobile")]
[Route("mobile/[controller]/[action]")]
public class AuthController : Controller
{
    private readonly IAuthService _authService;
    private readonly IRiderAuthService _riderAuthService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        IRiderAuthService riderAuthService,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _riderAuthService = riderAuthService;
        _logger = logger;
    }

    // ── REGISTER (Regular User) ──────────────────────────────────────────

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            // Riders should not be in the mobile user area
            if (User.HasClaim(c => c.Type == "IsRider" && c.Value == "true"))
                return RedirectToAction("Index", "Rider");

            return RedirectToAction("Index", "Home", new { area = "Mobile" });
        }

        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterDTO dto)
    {
        if (!ModelState.IsValid)
            return View(dto);

        var user = await _authService.RegisterAsync(dto);

        if (user is null)
        {
            ModelState.AddModelError(nameof(dto.Email), "An account with this email already exists.");
            return View(dto);
        }

        await SignInUserAsync(user, rememberMe: false);

        _logger.LogInformation("Mobile: User {UserId} registered and signed in.", user.Id);
        return RedirectToAction("Index", "Home", new { area = "Mobile" });
    }

    // ── RIDER REGISTER ────────────────────────────────────────────────────

    [HttpGet]
    [AllowAnonymous]
    public IActionResult RiderRegister()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            // Already logged in riders go to their dashboard
            if (User.HasClaim(c => c.Type == "IsRider" && c.Value == "true"))
                return RedirectToAction("Index", "Rider");

            // Regular users go to mobile home
            return RedirectToAction("Index", "Home", new { area = "Mobile" });
        }

        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RiderRegister(RiderRegisterDTO dto)
    {
        if (!ModelState.IsValid)
            return View(dto);

        var rider = await _riderAuthService.RegisterAsync(dto);

        if (rider is null)
        {
            ModelState.AddModelError(nameof(dto.Email), "An account with this email already exists.");
            return View(dto);
        }

        await SignInRiderAsync(rider, rememberMe: false);

        _logger.LogInformation("Mobile: Rider {RiderId} registered and signed in.", rider.Id);
        return RedirectToAction("RiderApproval", "Auth", new { area = "Mobile" });
    }

    // ── RIDER APPROVAL STATUS ─────────────────────────────────────────────

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> RiderApproval()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var riderId))
            return RedirectToAction("Login", new { area = "Mobile" });

        var rider = await _riderAuthService.GetByIdAsync(riderId);
        if (rider == null)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", new { area = "Mobile" });
        }

        ViewBag.IsApproved = rider.IsApproved;
        ViewBag.RejectionReason = rider.RejectionReason;
        ViewBag.RejectedAt = rider.RejectedAt;
        ViewBag.ResubmittedAt = rider.ResubmittedAt;

        var canEdit = !rider.IsApproved
                      && !string.IsNullOrEmpty(rider.RejectionReason)
                      && (rider.ResubmittedAt == null || (rider.RejectedAt != null && rider.ResubmittedAt <= rider.RejectedAt));

        ViewBag.CanEdit = canEdit;

        return View();
    }

    // ── LOGIN ─────────────────────────────────────────────────────────────

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            // Admin → admin dashboard (main area, not mobile)
            if (User.IsInRole("Admin"))
                return RedirectToAction("Index", "Admin");

            // Rider → rider dashboard (main area, completely separate experience)
            var isRider = User.HasClaim(c => c.Type == "IsRider" && c.Value == "true");
            if (isRider)
            {
                var riderIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(riderIdClaim, out var riderId))
                {
                    var rider = await _riderAuthService.GetByIdAsync(riderId);
                    if (rider != null && !rider.IsApproved)
                        return RedirectToAction("RiderApproval", "Auth", new { area = "Mobile" });

                    // Approved rider → rider dashboard (main area)
                    return RedirectToAction("Index", "Rider");
                }
            }

            // Regular user → mobile home
            return RedirectToLocal(returnUrl);
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginDTO dto, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
            return View(dto);

        // First try regular user
        var user = await _authService.ValidateCredentialsAsync(dto);
        if (user is not null)
        {
            await SignInUserAsync(user, dto.RememberMe);
            _logger.LogInformation("Mobile: User {UserId} logged in.", user.Id);

            // Admin → admin dashboard (main area)
            if (user.Role == Enums.UserRole.Admin)
                return RedirectToAction("Index", "Admin");

            // Regular user → mobile home
            return RedirectToLocal(returnUrl);
        }

        // Then try rider
        var rider = await _riderAuthService.ValidateCredentialsAsync(dto);
        if (rider is not null)
        {
            if (!rider.IsApproved)
            {
                await SignInRiderAsync(rider, dto.RememberMe);
                _logger.LogInformation("Mobile: Unapproved rider {RiderId} logged in, redirecting to approval.", rider.Id);
                return RedirectToAction("RiderApproval", new { area = "Mobile" });
            }

            await SignInRiderAsync(rider, dto.RememberMe);
            _logger.LogInformation("Mobile: Rider {RiderId} logged in, redirecting to rider dashboard.", rider.Id);
            // Approved riders go to their dedicated dashboard (main area, not mobile)
            return RedirectToAction("Index", "Rider");
        }

        ModelState.AddModelError(string.Empty, "Invalid email or password.");
        return View(dto);
    }

    // ── FORGOT PASSWORD ───────────────────────────────────────────────────

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ForgotPassword()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            // Riders go to their dashboard
            if (User.HasClaim(c => c.Type == "IsRider" && c.Value == "true"))
                return RedirectToAction("Index", "Rider");

            return RedirectToAction("Index", "Home", new { area = "Mobile" });
        }

        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordDTO dto)
    {
        if (!ModelState.IsValid)
            return View(dto);

        // Build reset URL pointing to the mobile area
        var resetUrl = Url.Action(
            nameof(ResetPassword), "Auth",
            new { area = "Mobile" },
            protocol: Request.Scheme)!;

        await _authService.ForgotPasswordAsync(dto, resetUrl);

        TempData["ForgotPasswordConfirm"] =
            "If an account with that email exists, a reset link has been sent.";

        return RedirectToAction(nameof(ForgotPasswordConfirmation));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ForgotPasswordConfirmation()
    {
        return View();
    }

    // ── RESET PASSWORD ────────────────────────────────────────────────────

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ResetPassword(string? token, string? email)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(email))
        {
            TempData["ResetError"] = "This password reset link is invalid or has expired.";
            return RedirectToAction(nameof(Login));
        }

        return View(new ResetPasswordDTO { Token = token, Email = email });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordDTO dto)
    {
        if (!ModelState.IsValid)
            return View(dto);

        var success = await _authService.ResetPasswordAsync(dto);

        if (!success)
        {
            ModelState.AddModelError(string.Empty,
                "This reset link is invalid or has expired. Please request a new one.");
            return View(dto);
        }

        TempData["ResetSuccess"] = "Your password has been updated. You can now sign in.";
        return RedirectToAction(nameof(Login));
    }

    // ── GOOGLE OAUTH ──────────────────────────────────────────────────────

    [HttpGet]
    [AllowAnonymous]
    public IActionResult GoogleLogin(string? returnUrl = null)
    {
        var redirectUrl = Url.Action(
            nameof(ExternalLoginCallback), "Auth",
            new { area = "Mobile", returnUrl });

        var properties = new AuthenticationProperties
        {
            RedirectUri = redirectUrl
        };

        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null)
    {
        var externalResult = await HttpContext.AuthenticateAsync("ExternalCookie");

        if (!externalResult.Succeeded || externalResult.Principal is null)
        {
            _logger.LogWarning("Mobile: External authentication failed.");
            TempData["ExternalAuthError"] = "Google sign-in failed. Please try again.";
            return RedirectToAction(nameof(Login));
        }

        var email = externalResult.Principal.FindFirstValue(ClaimTypes.Email);

        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogWarning("Mobile: Google did not return an email address.");
            TempData["ExternalAuthError"] = "Google did not provide an email address. Please try again.";
            return RedirectToAction(nameof(Login));
        }

        await HttpContext.SignOutAsync("ExternalCookie");

        var user = await _authService.FindOrCreateGoogleUserAsync(email);
        await SignInUserAsync(user, rememberMe: false);

        _logger.LogInformation("Mobile: User {UserId} signed in via Google.", user.Id);

        // Admin → admin dashboard (main area)
        if (user.Role == Enums.UserRole.Admin)
            return RedirectToAction("Index", "Admin");

        return RedirectToLocal(returnUrl);
    }

    // ── LOGOUT ────────────────────────────────────────────────────────────

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        _logger.LogInformation("Mobile: User signed out.");
        return RedirectToAction(nameof(Login));
    }

    // ── HELPERS ───────────────────────────────────────────────────────────

    private async Task SignInUserAsync(User user, bool rememberMe)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email,          user.Email),
            new(ClaimTypes.Name,           user.Email),
            new(ClaimTypes.Role,           user.Role.ToString()),
            new("IsRider", "false")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = rememberMe,
            ExpiresUtc = rememberMe
                ? DateTimeOffset.UtcNow.AddDays(30)
                : DateTimeOffset.UtcNow.AddHours(2)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            authProperties);
    }

    private async Task SignInRiderAsync(Rider rider, bool rememberMe)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, rider.Id.ToString()),
            new(ClaimTypes.Email,          rider.Email),
            new(ClaimTypes.Name,           rider.Email),
            new("IsRider", "true"),
            new("FullName", rider.FullName ?? rider.Email)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = rememberMe,
            ExpiresUtc = rememberMe
                ? DateTimeOffset.UtcNow.AddDays(30)
                : DateTimeOffset.UtcNow.AddHours(2)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            authProperties);
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Index", "Home", new { area = "Mobile" });
    }
}