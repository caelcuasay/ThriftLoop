using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;
using ThriftLoop.ViewModels;

namespace ThriftLoop.Controllers;

public class HomeController : BaseController
{
    private readonly IItemRepository _itemRepository;
    private readonly IItemLikeRepository _likeRepository;
    private readonly ILogger<HomeController> _logger;

    public HomeController(IItemRepository itemRepository, IItemLikeRepository likeRepository, ILogger<HomeController> logger)
    {
        _itemRepository = itemRepository;
        _likeRepository = likeRepository;
        _logger = logger;
    }

    // ── For You feed ───────────────────────────────────────────────────────

    /// <summary>
    /// GET / — public feed showing all listed items.
    /// Riders are redirected to their dashboard.
    /// Authenticated users see ownership state per card (no buy buttons on own items).
    /// Anonymous visitors see buy buttons on every card.
    /// </summary>
    public async Task<IActionResult> Index()
    {
        // Redirect admins to admin dashboard
        if (User.Identity?.IsAuthenticated == true && User.IsInRole("Admin"))
        {
            _logger.LogInformation("Admin attempted to access Home/Index, redirecting to Admin dashboard.");
            return RedirectToAction("Index", "Admin");
        }

        // Redirect riders to their dashboard
        if (User.Identity?.IsAuthenticated == true && User.HasClaim(c => c.Type == "IsRider" && c.Value == "true"))
        {
            _logger.LogInformation("Rider attempted to access Home/Index, redirecting to Rider dashboard.");
            return RedirectToAction("Index", "Rider");
        }

        var items = await _itemRepository.GetAllAsync();

        // Calculate price ranges for shop items
        var priceDisplayDict = new Dictionary<int, string>();
        foreach (var item in items.Where(i => i.ShopId.HasValue))
        {
            priceDisplayDict[item.Id] = await GetItemPriceDisplayAsync(item);
        }

        var userId = ResolveUserId();

        // Fetch liked items and like counts
        var likedItemIds = new HashSet<int>();
        var likeCounts = new Dictionary<int, int>();

        foreach (var item in items)
        {
            likeCounts[item.Id] = await _likeRepository.GetCountByItemIdAsync(item.Id);
        }

        if (userId.HasValue)
        {
            var likes = await _likeRepository.GetByUserIdAsync(userId.Value);
            likedItemIds = new HashSet<int>(likes.Select(l => l.ItemId));
        }

        var viewModel = new HomeIndexViewModel
        {
            Items = items,
            CurrentUserId = userId,
            ShopItemPriceDisplay = priceDisplayDict,
            LikedItemIds = likedItemIds,
            LikeCounts = likeCounts
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