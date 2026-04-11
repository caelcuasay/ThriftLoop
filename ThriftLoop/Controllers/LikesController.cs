using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThriftLoop.Enums;
using ThriftLoop.Repositories.Interface;
using ThriftLoop.ViewModels;

namespace ThriftLoop.Controllers;

[Authorize]
public class LikesController : BaseController
{
    private readonly IItemLikeRepository _likeRepository;
    private readonly ILogger<LikesController> _logger;

    public LikesController(
        IItemLikeRepository likeRepository,
        ILogger<LikesController> logger)
    {
        _likeRepository = likeRepository;
        _logger = logger;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  LIKED ITEMS INDEX
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> Index(string? filter = null)
    {
        var userId = ResolveUserId();
        if (userId is null) return Unauthorized();

        var likedItems = await _likeRepository.GetByUserIdAsync(userId.Value);

        var viewModels = likedItems.Select(il => new LikedItemViewModel
        {
            ItemId = il.ItemId,
            Title = il.Item?.Title ?? "Unknown Item",
            ImageUrl = il.Item?.ImageUrl,
            Price = il.Item?.Price ?? 0,
            Category = il.Item?.Category ?? "",
            Condition = il.Item?.Condition ?? "",
            Status = il.Item?.Status ?? ItemStatus.Available,
            SellerId = il.Item?.UserId ?? 0,
            SellerName = il.Item?.User?.Email?.Split('@')[0] ?? "Unknown",
            ShopId = il.Item?.ShopId,
            ShopName = il.Item?.Shop?.ShopName,
            LikedAt = il.LikedAt,
            LikeCount = 0 // Will be populated separately
        }).ToList();

        // Get like counts for all items
        var itemIds = viewModels.Select(v => v.ItemId).ToList();
        var likeCounts = await _likeRepository.GetLikeCountsAsync(itemIds);

        foreach (var vm in viewModels)
        {
            vm.LikeCount = likeCounts.GetValueOrDefault(vm.ItemId, 0);
        }

        // Apply filter if specified
        if (filter == "p2p")
        {
            viewModels = viewModels.Where(v => v.IsP2P).ToList();
        }
        else if (filter == "shop")
        {
            viewModels = viewModels.Where(v => !v.IsP2P).ToList();
        }
        else if (filter == "available")
        {
            viewModels = viewModels.Where(v => v.IsAvailable).ToList();
        }

        var viewModel = new LikedItemsViewModel
        {
            Items = viewModels,
            CurrentFilter = filter
        };

        return View(viewModel);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  TOGGLE LIKE (AJAX)
    // ══════════════════════════════════════════════════════════════════════

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle([FromBody] ToggleLikeDto dto)
    {
        var userId = ResolveUserId();
        if (userId is null)
            return Json(new LikeToggleResponse { Success = false, Error = "Not authenticated." });

        var isLiked = await _likeRepository.HasLikedAsync(userId.Value, dto.ItemId);

        if (isLiked)
        {
            // Unlike
            await _likeRepository.RemoveAsync(userId.Value, dto.ItemId);
            _logger.LogInformation("User {UserId} unliked Item {ItemId}", userId.Value, dto.ItemId);
        }
        else
        {
            // Like
            var result = await _likeRepository.AddAsync(userId.Value, dto.ItemId);
            if (result is null)
                return Json(new LikeToggleResponse { Success = false, Error = "Already liked." });

            _logger.LogInformation("User {UserId} liked Item {ItemId}", userId.Value, dto.ItemId);
        }

        var likeCount = await _likeRepository.GetCountByItemIdAsync(dto.ItemId);

        return Json(new LikeToggleResponse
        {
            Success = true,
            IsLiked = !isLiked,
            LikeCount = likeCount
        });
    }

    // ══════════════════════════════════════════════════════════════════════
    //  CHECK LIKE STATUS (AJAX)
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> Status(int itemId)
    {
        var userId = ResolveUserId();
        if (userId is null)
            return Json(new { isLiked = false, count = 0 });

        var isLiked = await _likeRepository.HasLikedAsync(userId.Value, itemId);
        var count = await _likeRepository.GetCountByItemIdAsync(itemId);

        return Json(new { isLiked, count });
    }

    // ══════════════════════════════════════════════════════════════════════
    //  BATCH LIKE STATUS (AJAX - for listing pages)
    // ══════════════════════════════════════════════════════════════════════

    [HttpPost]
    public async Task<IActionResult> BatchStatus([FromBody] int[] itemIds)
    {
        var userId = ResolveUserId();
        if (userId is null || itemIds.Length == 0)
            return Json(new Dictionary<int, bool>());

        // Get all likes for this user in one query
        var likedItems = await _likeRepository.GetByUserIdAsync(userId.Value);
        var likedItemIds = likedItems.Select(l => l.ItemId).ToHashSet();

        var result = itemIds.ToDictionary(
            id => id,
            id => likedItemIds.Contains(id)
        );

        return Json(result);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  MOST LIKED ITEMS (Public - for trending/most liked feed)
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> MostLiked(int count = 10, string? type = null)
    {
        bool? p2pOnly = type?.ToLower() switch
        {
            "p2p" => true,
            "shop" => false,
            _ => null
        };

        var mostLiked = await _likeRepository.GetMostLikedAsync(count, p2pOnly);

        // Check if current user has liked each item
        var userId = ResolveUserId();
        var userLikedItemIds = new HashSet<int>();

        if (userId.HasValue)
        {
            var userLikes = await _likeRepository.GetByUserIdAsync(userId.Value);
            userLikedItemIds = userLikes.Select(l => l.ItemId).ToHashSet();
        }

        var viewModels = mostLiked.Select(x => new
        {
            ItemId = x.Item.Id,
            Title = x.Item.Title,
            ImageUrl = x.Item.ImageUrl,
            Price = x.Item.Price,
            Category = x.Item.Category,
            Status = x.Item.Status,
            LikeCount = x.LikeCount,
            IsLiked = userLikedItemIds.Contains(x.Item.Id),
            IsP2P = x.Item.ShopId == null,
            ShopName = x.Item.Shop?.ShopName
        }).ToList();

        return Json(viewModels);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  REMOVE FROM LIKES
    // ══════════════════════════════════════════════════════════════════════

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int itemId)
    {
        var userId = ResolveUserId();
        if (userId is null)
            return Unauthorized();

        await _likeRepository.RemoveAsync(userId.Value, itemId);

        _logger.LogInformation("User {UserId} removed Item {ItemId} from likes", userId.Value, itemId);

        TempData["InfoMessage"] = "Item removed from your likes.";
        return RedirectToAction(nameof(Index));
    }
}
