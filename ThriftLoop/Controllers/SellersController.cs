using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ThriftLoop.Models;
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

        // Calculate price ranges for shop items
        var priceDisplayDict = new Dictionary<int, string>();
        foreach (var item in items)
        {
            priceDisplayDict[item.Id] = await GetItemPriceDisplayAsync(item);
        }

        _logger.LogInformation(
            "Sellers feed loaded — {ItemCount} items, {ShopCount} shops.",
            items.Count, shops.Count);

        return View(new SellersViewModel
        {
            Items = items,
            Shops = shops,
            CurrentUserId = currentUserId,
            ShopItemPriceDisplay = priceDisplayDict
        });
    }

    private async Task<string> GetItemPriceDisplayAsync(Item item)
    {
        // Load variants and SKUs for price calculation
        var itemWithVariants = await _itemRepo.GetByIdWithVariantsAsync(item.Id);
        if (itemWithVariants == null || !itemWithVariants.Variants.Any())
        {
            return $"₱{item.Price:N2}";
        }

        var allSkus = itemWithVariants.Variants
            .SelectMany(v => v.Skus)
            .Where(s => s.Status == ThriftLoop.Enums.SkuStatus.Available)
            .ToList();

        if (!allSkus.Any())
        {
            return $"₱{item.Price:N2}";
        }

        var minPrice = allSkus.Min(s => s.Price);
        var maxPrice = allSkus.Max(s => s.Price);

        if (Math.Abs(minPrice - maxPrice) < 0.01m)
        {
            return $"₱{minPrice:N2}";
        }

        return $"₱{minPrice:N2} - ₱{maxPrice:N2}";
    }
}