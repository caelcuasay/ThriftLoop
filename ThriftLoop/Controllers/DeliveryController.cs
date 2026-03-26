using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ThriftLoop.Repositories.Interface;

namespace ThriftLoop.Controllers;

[Authorize]
public class DeliveryController : BaseController
{
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly ILogger<DeliveryController> _logger;

    public DeliveryController(
        IDeliveryRepository deliveryRepository,
        ILogger<DeliveryController> logger)
    {
        _deliveryRepository = deliveryRepository;
        _logger = logger;
    }

    /// <summary>
    /// Shows the rider's current active delivery, if any.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> MyActiveDelivery()
    {
        var riderIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(riderIdClaim, out var riderId))
            return RedirectToAction("Login", "Auth");

        var activeDelivery = await _deliveryRepository.GetActiveDeliveryByRiderIdAsync(riderId);

        if (activeDelivery == null)
        {
            TempData["InfoMessage"] = "You don't have an active delivery at the moment.";
            return RedirectToAction("Index", "Rider");
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

        var success = await _deliveryRepository.MarkPickedUpAsync(deliveryId, riderId);

        if (!success)
        {
            TempData["ErrorMessage"] = "Unable to mark as picked up. Please ensure you have an active delivery.";
            return RedirectToAction("MyActiveDelivery");
        }

        _logger.LogInformation("Rider {RiderId} marked delivery {DeliveryId} as picked up.", riderId, deliveryId);
        TempData["SuccessMessage"] = "Item marked as picked up. Please deliver it to the buyer.";

        return RedirectToAction("MyActiveDelivery");
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

        var success = await _deliveryRepository.MarkDeliveredAsync(deliveryId, riderId);

        if (!success)
        {
            TempData["ErrorMessage"] = "Unable to mark as delivered. Please ensure the item was picked up first.";
            return RedirectToAction("MyActiveDelivery");
        }

        _logger.LogInformation("Rider {RiderId} marked delivery {DeliveryId} as delivered.", riderId, deliveryId);
        TempData["SuccessMessage"] = "Item marked as delivered. Waiting for buyer confirmation.";

        return RedirectToAction("MyActiveDelivery");
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

        var deliveries = await _deliveryRepository.GetDeliveriesByRiderIdAsync(riderId);
        return View(deliveries);
    }
}