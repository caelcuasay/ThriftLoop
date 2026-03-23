using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ThriftLoop.DTOs.Auth;
using ThriftLoop.Services.Auth.Interface;

namespace ThriftLoop.Controllers;

public class AuthController : Controller
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    // ─────────────────────────────────────────
    //  REGISTER
    // ─────────────────────────────────────────

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

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

        await SignInUserAsync(user.Id, user.Email, rememberMe: false);

        _logger.LogInformation("User {UserId} registered and signed in.", user.Id);
        return RedirectToAction("Index", "Home");
    }

    // ─────────────────────────────────────────
    //  LOGIN
    // ─────────────────────────────────────────

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToLocal(returnUrl);

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

        var user = await _authService.ValidateCredentialsAsync(dto);

        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(dto);
        }

        await SignInUserAsync(user.Id, user.Email, dto.RememberMe);

        _logger.LogInformation("User {UserId} logged in.", user.Id);
        return RedirectToLocal(returnUrl);
    }

    // ─────────────────────────────────────────
    //  FORGOT PASSWORD
    // ─────────────────────────────────────────

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ForgotPassword()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordDTO dto)
    {
        if (!ModelState.IsValid)
            return View(dto);

        var resetUrl = Url.Action(
            nameof(ResetPassword), "Auth",
            values: null,
            protocol: Request.Scheme)!;

        await _authService.ForgotPasswordAsync(dto, resetUrl);

        // Always show the confirmation — never reveal whether the email exists
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

    // ─────────────────────────────────────────
    //  RESET PASSWORD
    // ─────────────────────────────────────────

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

    // ─────────────────────────────────────────
    //  GOOGLE OAUTH
    // ─────────────────────────────────────────

    [HttpGet]
    [AllowAnonymous]
    public IActionResult GoogleLogin(string? returnUrl = null)
    {
        var redirectUrl = Url.Action(
            nameof(ExternalLoginCallback), "Auth", new { returnUrl });

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
            _logger.LogWarning("External authentication failed or was cancelled.");
            TempData["ExternalAuthError"] = "Google sign-in failed. Please try again.";
            return RedirectToAction(nameof(Login));
        }

        var email = externalResult.Principal.FindFirstValue(ClaimTypes.Email);

        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogWarning("Google did not return an email address.");
            TempData["ExternalAuthError"] = "Google did not provide an email address. Please try again.";
            return RedirectToAction(nameof(Login));
        }

        await HttpContext.SignOutAsync("ExternalCookie");

        var user = await _authService.FindOrCreateGoogleUserAsync(email);

        await SignInUserAsync(user.Id, user.Email, rememberMe: false);

        _logger.LogInformation("User {UserId} signed in via Google.", user.Id);
        return RedirectToLocal(returnUrl);
    }

    // ─────────────────────────────────────────
    //  LOGOUT
    // ─────────────────────────────────────────

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        _logger.LogInformation("User signed out.");
        return RedirectToAction(nameof(Login));
    }

    // ─────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────

    private async Task SignInUserAsync(int userId, string email, bool rememberMe)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, email)
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

        return RedirectToAction("Index", "Home");
    }
}