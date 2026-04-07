using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ThriftLoop.Repositories.Interface;
using ThriftLoop.Services.Auth.Interface;

namespace ThriftLoop.Controllers;

[Authorize]
public class RiderController : BaseController
{
    private readonly IRiderAuthService _riderAuthService;
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly ILogger<RiderController> _logger;

    public RiderController(
        IRiderAuthService riderAuthService,
        IDeliveryRepository deliveryRepository,
        ILogger<RiderController> logger)
    {
        _riderAuthService = riderAuthService;
        _deliveryRepository = deliveryRepository;
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
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Auth");
        }

        // Check if rider is approved - if not, redirect to approval page
        if (!rider.IsApproved)
        {
            _logger.LogInformation("Unapproved rider {RiderId} attempted to access dashboard, redirecting to approval page.", riderId);
            return RedirectToAction("RiderApproval", "Auth");
        }

        // Check if rider has an active delivery
        var activeDelivery = await _deliveryRepository.GetActiveDeliveryByRiderIdAsync(riderId);

        if (activeDelivery != null)
        {
            // Rider is already on a delivery - show active delivery view
            var delivery = await _deliveryRepository.GetByIdWithDetailsAsync(activeDelivery.Id);
            ViewBag.RiderName = rider.FullName;
            return View("ActiveDelivery", delivery);
        }

        // Show available job listings
        var availableDeliveries = await _deliveryRepository.GetAvailableDeliveriesAsync();
        ViewBag.RiderName = rider.FullName;
        return View(availableDeliveries);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptJob(int deliveryId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var riderId))
            return RedirectToAction("Login", "Auth");

        var rider = await _riderAuthService.GetByIdAsync(riderId);
        if (rider == null || !rider.IsApproved)
        {
            return RedirectToAction("Login", "Auth");
        }

        var success = await _deliveryRepository.AcceptDeliveryAsync(deliveryId, riderId);

        if (!success)
        {
            TempData["ErrorMessage"] = "Unable to accept this delivery. It may have been taken by another rider, or you already have an active delivery.";
            return RedirectToAction(nameof(Index));
        }

        _logger.LogInformation("Rider {RiderId} accepted delivery {DeliveryId}.", riderId, deliveryId);
        TempData["SuccessMessage"] = "Delivery accepted! Please proceed to pick up the item from the seller.";

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Shows the rider's current active delivery, if any.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ActiveDelivery()
    {
        var riderIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(riderIdClaim, out var riderId))
            return RedirectToAction("Login", "Auth");

        var rider = await _riderAuthService.GetByIdAsync(riderId);
        if (rider == null || !rider.IsApproved)
        {
            return RedirectToAction("Login", "Auth");
        }

        var activeDelivery = await _deliveryRepository.GetActiveDeliveryByRiderIdAsync(riderId);

        if (activeDelivery == null)
        {
            TempData["InfoMessage"] = "You don't have an active delivery at the moment.";
            return RedirectToAction(nameof(Index));
        }

        var delivery = await _deliveryRepository.GetByIdWithDetailsAsync(activeDelivery.Id);
        return View(delivery);
    }

    /// <summary>
    /// Marks the active delivery as picked up (rider has collected item from seller).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkPickedUp(int deliveryId)
    {
        var riderIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(riderIdClaim, out var riderId))
            return RedirectToAction("Login", "Auth");

        var rider = await _riderAuthService.GetByIdAsync(riderId);
        if (rider == null || !rider.IsApproved)
        {
            return RedirectToAction("Login", "Auth");
        }

        var success = await _deliveryRepository.MarkPickedUpAsync(deliveryId, riderId);

        if (!success)
        {
            TempData["ErrorMessage"] = "Unable to mark as picked up. Please ensure you have an active delivery.";
            return RedirectToAction(nameof(ActiveDelivery));
        }

        _logger.LogInformation("Rider {RiderId} marked delivery {DeliveryId} as picked up.", riderId, deliveryId);
        TempData["SuccessMessage"] = "Item marked as picked up. Please deliver it to the buyer.";

        return RedirectToAction(nameof(ActiveDelivery));
    }

    /// <summary>
    /// Marks the active delivery as delivered (rider has handed item to buyer).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkDelivered(int deliveryId)
    {
        var riderIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(riderIdClaim, out var riderId))
            return RedirectToAction("Login", "Auth");

        var rider = await _riderAuthService.GetByIdAsync(riderId);
        if (rider == null || !rider.IsApproved)
        {
            return RedirectToAction("Login", "Auth");
        }

        var success = await _deliveryRepository.MarkDeliveredAsync(deliveryId, riderId);

        if (!success)
        {
            TempData["ErrorMessage"] = "Unable to mark as delivered. Please ensure the item was picked up first.";
            return RedirectToAction(nameof(ActiveDelivery));
        }

        _logger.LogInformation("Rider {RiderId} marked delivery {DeliveryId} as delivered.", riderId, deliveryId);
        TempData["SuccessMessage"] = "Item marked as delivered. Waiting for buyer confirmation.";

        return RedirectToAction(nameof(ActiveDelivery));
    }

    /// <summary>
    /// Shows all deliveries completed by this rider.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> History()
    {
        var riderIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(riderIdClaim, out var riderId))
            return RedirectToAction("Login", "Auth");

        var rider = await _riderAuthService.GetByIdAsync(riderId);
        if (rider == null || !rider.IsApproved)
        {
            return RedirectToAction("Login", "Auth");
        }

        var deliveries = await _deliveryRepository.GetDeliveriesByRiderIdAsync(riderId);
        return View(deliveries);
    }
}