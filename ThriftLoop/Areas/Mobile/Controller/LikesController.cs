// Areas/Mobile/Controllers/LikesController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThriftLoop.Controllers;
using ThriftLoop.Enums;
using ThriftLoop.Repositories.Interface;
using ThriftLoop.ViewModels;

namespace ThriftLoop.Areas.Mobile.Controllers;

[Area("Mobile")]
[Route("mobile/[controller]/[action]")]
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

    [HttpGet]
    public async Task<IActionResult> Index(string? filter = null)
    {
        var userId = ResolveUserId();
        if (userId is null) return Unauthorized();

        // Redirect riders
        if (User.HasClaim(c => c.Type == "IsRider" && c.Value == "true"))
        {
            _logger.LogWarning("Mobile: Rider attempted to access Likes/Index");
            return RedirectToAction("Index", "Home", new { area = "Mobile" });
        }

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
            LikeCount = 0
        }).ToList();

        var itemIds = viewModels.Select(v => v.ItemId).ToList();
        var likeCounts = await _likeRepository.GetLikeCountsAsync(itemIds);
        foreach (var vm in viewModels)
            vm.LikeCount = likeCounts.GetValueOrDefault(vm.ItemId, 0);

        if (filter == "p2p")
            viewModels = viewModels.Where(v => v.IsP2P).ToList();
        else if (filter == "shop")
            viewModels = viewModels.Where(v => !v.IsP2P).ToList();
        else if (filter == "available")
            viewModels = viewModels.Where(v => v.IsAvailable).ToList();

        var viewModel = new LikedItemsViewModel
        {
            Items = viewModels,
            CurrentFilter = filter
        };

        return View(viewModel);
    }
}