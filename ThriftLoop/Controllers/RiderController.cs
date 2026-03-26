using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ThriftLoop.Services.Auth.Interface;

namespace ThriftLoop.Controllers;

[Authorize]
public class RiderController : BaseController
{
    private readonly IRiderAuthService _riderAuthService;
    private readonly ILogger<RiderController> _logger;

    public RiderController(
        IRiderAuthService riderAuthService,
        ILogger<RiderController> logger)
    {
        _riderAuthService = riderAuthService;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var riderId))
        {
            _logger.LogWarning("Rider attempted to access dashboard without valid ID claim.");
            return RedirectToAction("Login", "Auth");
        }

        var rider = await _riderAuthService.GetByIdAsync(riderId);
        if (rider == null)
        {
            _logger.LogWarning("Rider {RiderId} not found in database.", riderId);
            return RedirectToAction("Login", "Auth");
        }

        ViewBag.RiderName = rider.FullName;
        return View();
    }
}