// Areas/Mobile/Controllers/CartController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriftLoop.Constants;
using ThriftLoop.Controllers;
using ThriftLoop.Data;
using ThriftLoop.Enums;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;
using ThriftLoop.ViewModels;

namespace ThriftLoop.Areas.Mobile.Controllers;

[Area("Mobile")]
[Route("mobile/[controller]/[action]")]
[Authorize]
public class CartController : BaseController
{
    private readonly ICartRepository _cartRepository;
    private readonly IItemRepository _itemRepository;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CartController> _logger;

    public CartController(
        ICartRepository cartRepository,
        IItemRepository itemRepository,
        ApplicationDbContext context,
        ILogger<CartController> logger)
    {
        _cartRepository = cartRepository;
        _itemRepository = itemRepository;
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // Redirect riders
        if (User.HasClaim(c => c.Type == "IsRider" && c.Value == "true"))
        {
            _logger.LogWarning("Mobile: Rider attempted to access Cart.");
            return RedirectToAction("Index", "Home", new { area = "Mobile" });
        }

        var userId = ResolveUserId();
        if (userId is null) return Unauthorized();

        var cartItems = await _cartRepository.GetByUserIdAsync(userId.Value);
        var viewModel = BuildCartViewModel(cartItems);

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add([FromBody] AddToCartDto dto)
    {
        var userId = ResolveUserId();
        if (userId is null) return Json(new { success = false, error = "Not authenticated." });

        var item = await _itemRepository.GetByIdAsync(dto.ItemId);
        if (item is null) return Json(new { success = false, error = "Item not found." });
        if (item.ShopId is null) return Json(new { success = false, error = "P2P items cannot be added to cart." });
        if (item.Status == ItemStatus.Disabled) return Json(new { success = false, error = "This listing is currently unavailable." });
        if (item.UserId == userId.Value) return Json(new { success = false, error = "You cannot add your own items to cart." });

        var sku = await _context.ItemVariantSkus.FindAsync(dto.SkuId);
        if (sku is null) return Json(new { success = false, error = "Invalid variant/size selection." });
        if (sku.Status != SkuStatus.Available || sku.Quantity < dto.Quantity)
            return Json(new { success = false, error = "Item is out of stock." });

        var cartItem = await _cartRepository.AddAsync(userId.Value, dto.ItemId, dto.SkuId, dto.Quantity);
        var cartCount = await _cartRepository.GetCountByUserIdAsync(userId.Value);

        return Json(new { success = true, cartCount, cartItemId = cartItem.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateQuantity([FromBody] UpdateCartQuantityDto dto)
    {
        var userId = ResolveUserId();
        if (userId is null) return Json(new CartOperationResponse { Success = false, Error = "Not authenticated." });

        var cartItem = await _cartRepository.GetByIdAsync(dto.CartItemId);
        if (cartItem is null || cartItem.UserId != userId.Value)
            return Json(new CartOperationResponse { Success = false, Error = "Cart item not found." });

        var sku = await _context.ItemVariantSkus
            .Include(s => s.Variant).ThenInclude(v => v.Item)
            .FirstOrDefaultAsync(s => s.Id == cartItem.ItemVariantSkuId);

        if (sku is null) return Json(new CartOperationResponse { Success = false, Error = "Item variant not found." });
        if (sku.Variant?.Item?.Status == ItemStatus.Disabled)
            return Json(new CartOperationResponse { Success = false, Error = "This listing is no longer available." });

        var quantity = Math.Max(1, Math.Min(dto.Quantity, sku.Quantity));
        await _cartRepository.UpdateQuantityAsync(dto.CartItemId, quantity);

        var cartItems = await _cartRepository.GetByUserIdAsync(userId.Value);
        var viewModel = BuildCartViewModel(cartItems);
        var updatedItem = viewModel.Items.FirstOrDefault(i => i.CartItemId == dto.CartItemId);
        var lineTotal = updatedItem?.LineTotal ?? (sku.Price * quantity);

        return Json(new CartOperationResponse
        {
            Success = true,
            Quantity = quantity,
            LineTotal = lineTotal.ToString("N2"),
            Subtotal = viewModel.Subtotal.ToString("N2"),
            TotalDeliveryFee = viewModel.TotalDeliveryFee.ToString("N2"),
            GrandTotal = viewModel.GrandTotal.ToString("N2"),
            CartCount = viewModel.TotalItems,
            IsEmpty = viewModel.IsEmpty,
            UniqueShopCount = viewModel.UniqueShopCount
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int cartItemId)
    {
        var userId = ResolveUserId();
        if (userId is null) return Json(new CartOperationResponse { Success = false, Error = "Not authenticated." });

        var cartItem = await _cartRepository.GetByIdAsync(cartItemId);
        if (cartItem is null || cartItem.UserId != userId.Value)
            return Json(new CartOperationResponse { Success = false, Error = "Cart item not found." });

        await _cartRepository.RemoveAsync(cartItemId);

        var cartItems = await _cartRepository.GetByUserIdAsync(userId.Value);
        var viewModel = BuildCartViewModel(cartItems);

        return Json(new CartOperationResponse
        {
            Success = true,
            Subtotal = viewModel.Subtotal.ToString("N2"),
            TotalDeliveryFee = viewModel.TotalDeliveryFee.ToString("N2"),
            GrandTotal = viewModel.GrandTotal.ToString("N2"),
            CartCount = viewModel.TotalItems,
            IsEmpty = viewModel.IsEmpty,
            UniqueShopCount = viewModel.UniqueShopCount
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Clear()
    {
        var userId = ResolveUserId();
        if (userId is null) return Unauthorized();

        await _cartRepository.ClearByUserIdAsync(userId.Value);
        TempData["InfoMessage"] = "Your cart has been cleared.";
        return RedirectToAction(nameof(Index));
    }

    private static CartIndexViewModel BuildCartViewModel(IReadOnlyList<CartItem> cartItems)
    {
        var items = cartItems.Select(ci =>
        {
            var item = ci.Item;
            var sku = ci.ItemVariantSku;
            return new CartItemViewModel
            {
                CartItemId = ci.Id,
                ItemId = ci.ItemId,
                ItemVariantSkuId = ci.ItemVariantSkuId,
                ItemTitle = item?.Title ?? "Unknown Item",
                ItemImageUrl = item?.ImageUrl,
                Category = item?.Category ?? "",
                Condition = item?.Condition ?? "",
                ShopId = item?.ShopId,
                ShopName = item?.Shop?.ShopName,
                SellerId = item?.UserId ?? 0,
                VariantName = sku?.Variant?.Name ?? "",
                Size = sku?.Size,
                Price = sku?.Price ?? 0,
                AvailableStock = sku?.Quantity ?? 0,
                Quantity = ci.Quantity,
                AddedAt = ci.AddedAt,
                IsItemDisabled = item?.Status == ItemStatus.Disabled
            };
        }).ToList();

        return new CartIndexViewModel { Items = items, DeliveryFeePerShop = ItemConstants.DeliveryFee };
    }
}