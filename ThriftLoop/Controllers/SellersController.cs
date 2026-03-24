using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ThriftLoop.Repositories.Interface;
using ThriftLoop.ViewModels;

namespace ThriftLoop.Controllers;

/// <summary>
/// Public Sellers discovery page — no authentication required.
/// Shows all shop listings and a horizontal row of shop profiles to visit.
/// </summary>
public class SellersController : Controller
{
    private readonly IItemRepository _itemRepo;
    private readonly IShopRepository _shopRepo;
    private readonly ILogger<SellersController> _logger;

    public SellersController(
        IItemRepository itemRepo,
        IShopRepository shopRepo,
        ILogger<SellersController> logger)
    {
        _itemRepo = itemRepo;
        _shopRepo = shopRepo;
        _logger = logger;
    }

    // GET /Sellers
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var items = await _itemRepo.GetAllShopItemsAsync();
        var shops = await _shopRepo.GetAllApprovedAsync();

        int? currentUserId = null;
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(raw, out var parsed)) currentUserId = parsed;

        _logger.LogInformation(
            "Sellers feed loaded — {ItemCount} items, {ShopCount} shops.",
            items.Count, shops.Count);

        return View(new SellersViewModel
        {
            Items = items,
            Shops = shops,
            CurrentUserId = currentUserId
        });
    }
}