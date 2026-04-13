using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriftLoop.Constants;
using ThriftLoop.Data;
using ThriftLoop.Enums;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;
using ThriftLoop.ViewModels;

namespace ThriftLoop.Controllers;

[Authorize]
public class CartController : BaseController
{
    private readonly ICartRepository _cartRepository;
    private readonly IItemRepository _itemRepository;
    private readonly IItemLikeRepository _likeRepository;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CartController> _logger;

    public CartController(
        ICartRepository cartRepository,
        IItemRepository itemRepository,
        IItemLikeRepository likeRepository,
        ApplicationDbContext context,
        ILogger<CartController> logger)
    {
        _cartRepository = cartRepository;
        _itemRepository = itemRepository;
        _likeRepository = likeRepository;
        _context = context;
        _logger = logger;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  CART INDEX
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = ResolveUserId();
        if (userId is null) return Unauthorized();

        var cartItems = await _cartRepository.GetByUserIdAsync(userId.Value);
        var viewModel = BuildCartViewModel(cartItems);

        return View(viewModel);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  ADD TO CART (AJAX)
    // ══════════════════════════════════════════════════════════════════════

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add([FromBody] AddToCartDto dto)
    {
        var userId = ResolveUserId();
        if (userId is null)
            return Json(new { success = false, error = "Not authenticated." });

        // Validate: Only shop items can be added to cart
        var item = await _itemRepository.GetByIdAsync(dto.ItemId);
        if (item is null)
            return Json(new { success = false, error = "Item not found." });

        if (item.ShopId is null)
            return Json(new { success = false, error = "P2P items cannot be added to cart." });

        if (item.UserId == userId.Value)
            return Json(new { success = false, error = "You cannot add your own items to cart." });

        // Validate SKU exists and is available
        var sku = await _context.ItemVariantSkus.FindAsync(dto.SkuId);
        if (sku is null)
            return Json(new { success = false, error = "Invalid variant/size selection." });

        if (sku.Status != SkuStatus.Available || sku.Quantity < dto.Quantity)
            return Json(new { success = false, error = "Item is out of stock." });

        // Add to cart
        var cartItem = await _cartRepository.AddAsync(userId.Value, dto.ItemId, dto.SkuId, dto.Quantity);
        var cartCount = await _cartRepository.GetCountByUserIdAsync(userId.Value);

        _logger.LogInformation(
            "User {UserId} added Item {ItemId} SKU {SkuId} ×{Qty} to cart. Cart total: {CartCount}",
            userId.Value, dto.ItemId, dto.SkuId, dto.Quantity, cartCount);

        return Json(new { success = true, cartCount, cartItemId = cartItem.Id });
    }

    // ══════════════════════════════════════════════════════════════════════
    //  UPDATE QUANTITY (AJAX)
    // ══════════════════════════════════════════════════════════════════════

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateQuantity([FromBody] UpdateCartQuantityDto dto)
    {
        var userId = ResolveUserId();
        if (userId is null)
            return Json(new CartOperationResponse { Success = false, Error = "Not authenticated." });

        // Verify ownership
        var cartItem = await _cartRepository.GetByIdAsync(dto.CartItemId);
        if (cartItem is null || cartItem.UserId != userId.Value)
            return Json(new CartOperationResponse { Success = false, Error = "Cart item not found." });

        // Validate stock availability
        var sku = await _context.ItemVariantSkus
            .Include(s => s.Variant)
                .ThenInclude(v => v.Item)
            .FirstOrDefaultAsync(s => s.Id == cartItem.ItemVariantSkuId);

        if (sku is null)
            return Json(new CartOperationResponse { Success = false, Error = "Item variant not found." });

        var quantity = Math.Max(1, Math.Min(dto.Quantity, sku.Quantity));

        await _cartRepository.UpdateQuantityAsync(dto.CartItemId, quantity);

        // Reload cart items to get accurate totals
        var cartItems = await _cartRepository.GetByUserIdAsync(userId.Value);
        var viewModel = BuildCartViewModel(cartItems);

        // Calculate the line total for this specific item
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

    // ══════════════════════════════════════════════════════════════════════
    //  REMOVE FROM CART (AJAX)
    // ══════════════════════════════════════════════════════════════════════

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int cartItemId)
    {
        var userId = ResolveUserId();
        if (userId is null)
            return Json(new CartOperationResponse { Success = false, Error = "Not authenticated." });

        // Verify ownership
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

    // ══════════════════════════════════════════════════════════════════════
    //  GET CART COUNT (AJAX - for navbar updates)
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> GetCartCount()
    {
        var userId = ResolveUserId();
        if (userId is null)
            return Json(new { count = 0 });

        var count = await _cartRepository.GetCountByUserIdAsync(userId.Value);
        return Json(new { count });
    }

    // ══════════════════════════════════════════════════════════════════════
    //  CLEAR CART
    // ══════════════════════════════════════════════════════════════════════

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Clear()
    {
        var userId = ResolveUserId();
        if (userId is null)
            return Unauthorized();

        await _cartRepository.ClearByUserIdAsync(userId.Value);

        _logger.LogInformation("User {UserId} cleared their cart.", userId.Value);

        TempData["InfoMessage"] = "Your cart has been cleared.";
        return RedirectToAction(nameof(Index));
    }

    // ══════════════════════════════════════════════════════════════════════
    //  PRIVATE HELPERS
    // ══════════════════════════════════════════════════════════════════════

    private static CartIndexViewModel BuildCartViewModel(IReadOnlyList<CartItem> cartItems)
    {
        var items = cartItems.Select(ci => new CartItemViewModel
        {
            CartItemId = ci.Id,
            ItemId = ci.ItemId,
            ItemVariantSkuId = ci.ItemVariantSkuId,
            ItemTitle = ci.Item?.Title ?? "Unknown Item",
            ItemImageUrl = ci.Item?.ImageUrl,
            Category = ci.Item?.Category ?? "",
            Condition = ci.Item?.Condition ?? "",
            ShopId = ci.Item?.ShopId,
            ShopName = ci.Item?.Shop?.ShopName,
            SellerId = ci.Item?.UserId ?? 0,
            VariantName = ci.ItemVariantSku?.Variant?.Name ?? "",
            Size = ci.ItemVariantSku?.Size,
            Price = ci.ItemVariantSku?.Price ?? 0,
            AvailableStock = ci.ItemVariantSku?.Quantity ?? 0,
            Quantity = ci.Quantity,
            AddedAt = ci.AddedAt
        }).ToList();

        return new CartIndexViewModel
        {
            Items = items,
            DeliveryFeePerShop = ItemConstants.DeliveryFee
        };
    }
}