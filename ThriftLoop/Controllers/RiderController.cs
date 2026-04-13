// Controllers/RiderController.cs
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ThriftLoop.Enums;
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

        if (!rider.IsApproved)
        {
            _logger.LogInformation("Unapproved rider {RiderId} attempted to access dashboard, redirecting to approval page.", riderId);
            return RedirectToAction("RiderApproval", "Auth");
        }

        var activeDelivery = await _deliveryRepository.GetActiveDeliveryByRiderIdAsync(riderId);

        if (activeDelivery != null)
        {
            var delivery = await _deliveryRepository.GetByIdWithDetailsAsync(activeDelivery.Id);
            ViewBag.RiderName = rider.FullName;
            return View("ActiveDelivery", delivery);
        }

        var availableDeliveries = await _deliveryRepository.GetAvailableDeliveriesAsync();
        ViewBag.RiderName = rider.FullName;
        ViewBag.RiderLatitude = rider.Latitude;
        ViewBag.RiderLongitude = rider.Longitude;

        return View(availableDeliveries);
    }

    [HttpGet]
    public async Task<IActionResult> DeliveryDetails(int id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var riderId))
            return RedirectToAction("Login", "Auth");

        var rider = await _riderAuthService.GetByIdAsync(riderId);
        if (rider == null || !rider.IsApproved)
            return RedirectToAction("Login", "Auth");

        var delivery = await _deliveryRepository.GetByIdWithDetailsAsync(id);

        if (delivery == null || delivery.Status != DeliveryStatus.Available)
        {
            TempData["ErrorMessage"] = "This delivery is no longer available.";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.RiderName = rider.FullName;
        ViewBag.RiderLatitude = rider.Latitude;
        ViewBag.RiderLongitude = rider.Longitude;

        return View(delivery);
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

        ViewBag.RiderName = rider.FullName;
        ViewBag.RiderLatitude = rider.Latitude;
        ViewBag.RiderLongitude = rider.Longitude;

        return View(delivery);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkPickedUp(int deliveryId)
    {
        var riderIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(riderIdClaim, out var riderId))
            return RedirectToAction("Login", "Auth");

        var success = await _deliveryRepository.MarkPickedUpAsync(deliveryId, riderId);

        if (!success)
        {
            TempData["ErrorMessage"] = "Unable to mark as picked up.";
            return RedirectToAction(nameof(ActiveDelivery));
        }

        TempData["SuccessMessage"] = "Item marked as picked up. Please deliver it to the buyer.";
        return RedirectToAction(nameof(ActiveDelivery));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkDelivered(int deliveryId)
    {
        var riderIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(riderIdClaim, out var riderId))
            return RedirectToAction("Login", "Auth");

        var success = await _deliveryRepository.MarkDeliveredAsync(deliveryId, riderId);

        if (!success)
        {
            TempData["ErrorMessage"] = "Unable to mark as delivered.";
            return RedirectToAction(nameof(ActiveDelivery));
        }

        TempData["SuccessMessage"] = "Item marked as delivered. Waiting for buyer confirmation.";
        return RedirectToAction(nameof(ActiveDelivery));
    }

    [HttpGet]
    public async Task<IActionResult> History()
    {
        var riderIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(riderIdClaim, out var riderId))
            return RedirectToAction("Login", "Auth");

        var deliveries = await _deliveryRepository.GetDeliveriesByRiderIdAsync(riderId);
        return View(deliveries);
    }

    [HttpGet]
    public async Task<IActionResult> EditApplication()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var riderId))
            return RedirectToAction("Login", "Auth");

        var rider = await _riderAuthService.GetRejectedApplicationAsync(riderId);
        if (rider == null)
        {
            if (rider?.IsApproved == true)
                return RedirectToAction(nameof(Index));
            return RedirectToAction("RiderApproval", "Auth");
        }

        var model = new ThriftLoop.DTOs.Auth.RiderEditDTO
        {
            Id = rider.Id,
            FullName = rider.FullName ?? "",
            Email = rider.Email,
            PhoneNumber = rider.PhoneNumber ?? "",
            Address = rider.Address ?? "",
            VehicleType = rider.VehicleType ?? "",
            VehicleColor = rider.VehicleColor ?? "",
            LicensePlate = rider.LicensePlate ?? "",
            ExistingLicenseUrl = rider.DriversLicense
        };

        ViewBag.RejectionReason = rider.RejectionReason;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditApplication(ThriftLoop.DTOs.Auth.RiderEditDTO model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.RejectionReason = "Please fix the errors below and resubmit.";
            return View(model);
        }

        var success = await _riderAuthService.UpdateApplicationAsync(model);

        if (!success)
        {
            TempData["ErrorMessage"] = "Failed to update your application. Please try again.";
            return View(model);
        }

        TempData["SuccessMessage"] = "Your application has been resubmitted for review.";
        return RedirectToAction("RiderApproval", "Auth");
    }
}