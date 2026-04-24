// Areas/Mobile/Controllers/SellersController.cs
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ThriftLoop.Controllers;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;
using ThriftLoop.ViewModels;

namespace ThriftLoop.Areas.Mobile.Controllers;

[Area("Mobile")]
[Route("mobile/[controller]/[action]")]
public class SellersController : BaseController
{
    private readonly IItemRepository _itemRepo;
    private readonly IShopRepository _shopRepo;
    private readonly IItemLikeRepository _likeRepo;
    private readonly ILogger<SellersController> _logger;

    public SellersController(
        IItemRepository itemRepo,
        IShopRepository shopRepo,
        IItemLikeRepository likeRepo,
        ILogger<SellersController> logger)
    {
        _itemRepo = itemRepo;
        _shopRepo = shopRepo;
        _likeRepo = likeRepo;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (User.Identity?.IsAuthenticated == true && User.HasClaim(c => c.Type == "IsRider" && c.Value == "true"))
        {
            _logger.LogInformation("Mobile: Rider attempted to access Sellers/Index, redirecting to Home.");
            return RedirectToAction("Index", "Home", new { area = "Mobile" });
        }

        var items = await _itemRepo.GetAllShopItemsAsync();
        var shops = await _shopRepo.GetAllApprovedAsync();

        int? currentUserId = ResolveUserId();

        var priceDisplayDict = new Dictionary<int, string>();
        foreach (var item in items)
        {
            priceDisplayDict[item.Id] = await GetItemPriceDisplayAsync(item);
        }

        var likedItemIds = new HashSet<int>();
        var likeCounts = new Dictionary<int, int>();

        foreach (var item in items)
        {
            likeCounts[item.Id] = await _likeRepo.GetCountByItemIdAsync(item.Id);
        }

        if (currentUserId.HasValue)
        {
            var likes = await _likeRepo.GetByUserIdAsync(currentUserId.Value);
            likedItemIds = new HashSet<int>(likes.Select(l => l.ItemId));
        }

        ViewBag.MaxPrice = items.Any() ? (int)Math.Ceiling((double)items.Max(i => i.Price)) : 100000;

        _logger.LogInformation("Mobile: Sellers feed loaded — {ItemCount} items, {ShopCount} shops.", items.Count, shops.Count);

        return View(new SellersViewModel
        {
            Items = items,
            Shops = shops,
            CurrentUserId = currentUserId,
            ShopItemPriceDisplay = priceDisplayDict,
            LikedItemIds = likedItemIds,
            LikeCounts = likeCounts
        });
    }

    private async Task<string> GetItemPriceDisplayAsync(Item item)
    {
        var itemWithVariants = await _itemRepo.GetByIdWithVariantsAsync(item.Id);
        if (itemWithVariants == null || !itemWithVariants.Variants.Any())
            return $"₱{item.Price:N2}";

        var allSkus = itemWithVariants.Variants
            .SelectMany(v => v.Skus)
            .Where(s => s.Status == ThriftLoop.Enums.SkuStatus.Available)
            .ToList();

        if (!allSkus.Any())
            return $"₱{item.Price:N2}";

        var minPrice = allSkus.Min(s => s.Price);
        var maxPrice = allSkus.Max(s => s.Price);

        if (Math.Abs(minPrice - maxPrice) < 0.01m)
            return $"₱{minPrice:N2}";

        return $"₱{minPrice:N2} - ₱{maxPrice:N2}";
    }
}