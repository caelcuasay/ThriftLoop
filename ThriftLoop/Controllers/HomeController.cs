using Microsoft.AspNetCore.Mvc;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;
using ThriftLoop.ViewModels;

namespace ThriftLoop.Controllers;

public class HomeController : BaseController
{
    private readonly IItemRepository _itemRepository;
    private readonly ILogger<HomeController> _logger;

    public HomeController(IItemRepository itemRepository, ILogger<HomeController> logger)
    {
        _itemRepository = itemRepository;
        _logger = logger;
    }

    // ── For You feed ───────────────────────────────────────────────────────

    /// <summary>
    /// GET / — public feed showing all listed items.
    /// Authenticated users see ownership state per card (no buy buttons on own items).
    /// Anonymous visitors see buy buttons on every card.
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var items = await _itemRepository.GetAllAsync();

        // Calculate price ranges for shop items
        var priceDisplayDict = new Dictionary<int, string>();
        foreach (var item in items.Where(i => i.ShopId.HasValue))
        {
            priceDisplayDict[item.Id] = await GetItemPriceDisplayAsync(item);
        }

        var viewModel = new HomeIndexViewModel
        {
            Items = items,
            CurrentUserId = ResolveUserId(),
            ShopItemPriceDisplay = priceDisplayDict
        };

        return View(viewModel);
    }

    private async Task<string> GetItemPriceDisplayAsync(Item item)
    {
        if (!item.ShopId.HasValue)
        {
            return $"₱{item.Price:N2}";
        }

        // Load variants and SKUs for price calculation
        var itemWithVariants = await _itemRepository.GetByIdWithVariantsAsync(item.Id);
        if (itemWithVariants == null || !itemWithVariants.Variants.Any())
        {
            return $"₱{item.Price:N2}";
        }

        var allSkus = itemWithVariants.Variants
            .SelectMany(v => v.Skus)
            .Where(s => s.Status == Enums.SkuStatus.Available)
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

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel
    {
        RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier
    });
}