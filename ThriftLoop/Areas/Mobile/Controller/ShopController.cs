// Areas/Mobile/Controllers/ShopController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ThriftLoop.Constants;
using ThriftLoop.Data;
using ThriftLoop.Enums;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;
using ThriftLoop.ViewModels;

namespace ThriftLoop.Areas.Mobile.Controllers;

[Area("Mobile")]
[Route("mobile/[controller]/[action]")]
public class ShopController : Controller
{
    private static readonly HashSet<string> _allowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/gif"
    };

    private readonly IShopRepository _shopRepo;
    private readonly IItemRepository _itemRepo;
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ShopController> _logger;

    public ShopController(
        IShopRepository shopRepo,
        IItemRepository itemRepo,
        ApplicationDbContext context,
        IWebHostEnvironment env,
        ILogger<ShopController> logger)
    {
        _shopRepo = shopRepo;
        _itemRepo = itemRepo;
        _context = context;
        _env = env;
        _logger = logger;
    }

    private int? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var id) ? id : null;
    }

    private async Task<IActionResult> RedirectIfRider()
    {
        if (User.HasClaim(c => c.Type == "IsRider" && c.Value == "true"))
        {
            _logger.LogWarning("Mobile: Rider attempted to access Shop page.");
            return RedirectToAction("Index", "Home", new { area = "Mobile" });
        }
        return null!;
    }

    // GET /mobile/Shop/Index/5
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Index(int id)
    {
        var shop = await _shopRepo.GetByIdAsync(id);
        if (shop is null) return NotFound();

        var currentUserId = GetCurrentUserId();
        var isOwner = currentUserId.HasValue && shop.UserId == currentUserId.Value;

        var items = await _itemRepo.GetByShopIdAsync(id);
        if (!isOwner)
            items = items.Where(i => i.Status != ItemStatus.Disabled).ToList();

        var soldCounts = new Dictionary<int, int>();
        foreach (var item in items)
            soldCounts[item.Id] = await GetSoldCountAsync(item.Id);

        var vm = new ShopPageViewModel
        {
            ShopId = shop.Id,
            IsOwner = isOwner,
            ShopName = shop.ShopName,
            Bio = shop.Bio,
            BannerUrl = shop.BannerUrl,
            LogoUrl = shop.LogoUrl,
            Latitude = shop.Latitude,
            Longitude = shop.Longitude,
            StoreAddress = shop.StoreAddress,
            Items = items,
            ItemSoldCounts = soldCounts
        };

        ViewBag.MaxPrice = items.Any() ? (int)Math.Ceiling((double)items.Max(i => i.Price)) : 100000;
        return View(vm);
    }

    // GET /mobile/Shop/Mine
    [HttpGet]
    [Authorize(Roles = "Seller")]
    public async Task<IActionResult> Mine()
    {
        var riderCheck = await RedirectIfRider();
        if (riderCheck is not null) return riderCheck;

        var userId = GetCurrentUserId();
        if (userId is null) return RedirectToAction("Login", "Auth", new { area = "Mobile" });

        var shop = await _shopRepo.GetByUserIdAsync(userId.Value);
        if (shop is null)
        {
            TempData["ShopError"] = "Your shop profile could not be found.";
            return RedirectToAction("Index", "Home", new { area = "Mobile" });
        }

        return RedirectToAction(nameof(Index), new { id = shop.Id });
    }

    // GET /mobile/Shop/Details/5
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var item = await _itemRepo.GetByIdWithVariantsAsync(id);
        if (item == null || item.ShopId == null) return NotFound();

        var shop = await _shopRepo.GetByIdAsync(item.ShopId.Value);
        if (shop == null) return NotFound();

        var currentUserId = GetCurrentUserId();

        var allSkus = item.Variants.SelectMany(v => v.Skus)
            .Where(s => s.Status == SkuStatus.Available).ToList();

        var startingPrice = allSkus.Any() ? allSkus.Min(s => s.Price) : item.Price;

        decimal? originalStartingPrice = null;
        if (item.HasActiveDiscount)
        {
            var skusWithOriginal = allSkus.Where(s => s.OriginalPrice.HasValue).ToList();
            originalStartingPrice = skusWithOriginal.Any()
                ? skusWithOriginal.Min(s => s.OriginalPrice!.Value)
                : item.OriginalPrice;
        }

        var viewModel = new ShopItemDetailsViewModel
        {
            ItemId = item.Id,
            ShopId = shop.Id,
            ShopName = shop.ShopName,
            ShopLogoUrl = shop.LogoUrl,
            IsOwner = currentUserId.HasValue && item.UserId == currentUserId.Value,
            Title = item.Title,
            Description = item.Description,
            Category = item.Category,
            Condition = item.Condition,
            StartingPrice = startingPrice,
            ImageUrls = item.ImageUrls,
            HasActiveDiscount = item.HasActiveDiscount,
            OriginalPrice = originalStartingPrice,
            DiscountPercentage = item.DiscountPercentage,
            DiscountExpiresAt = item.DiscountExpiresAt,
            Variants = item.Variants.Select(v => new ShopItemVariantViewModel
            {
                VariantId = v.Id,
                Name = v.Name,
                Skus = v.Skus.Select(s => new ShopItemSkuViewModel
                {
                    SkuId = s.Id,
                    Size = s.Size,
                    Price = s.Price,
                    Quantity = s.Quantity,
                    IsAvailable = s.Status == SkuStatus.Available && s.Quantity > 0,
                    OriginalPrice = s.OriginalPrice
                }).ToList()
            }).ToList()
        };

        return View(viewModel);
    }

    // GET /mobile/Shop/Create
    [HttpGet]
    [Authorize(Roles = "Seller")]
    public async Task<IActionResult> Create()
    {
        var riderCheck = await RedirectIfRider();
        if (riderCheck is not null) return riderCheck;

        var userId = GetCurrentUserId();
        if (userId is null) return RedirectToAction("Login", "Auth", new { area = "Mobile" });

        var shop = await _shopRepo.GetByUserIdAsync(userId.Value);
        if (shop is null) return RedirectToAction("Index", "Home", new { area = "Mobile" });

        return View(new ShopItemCreateViewModel { ShopId = shop.Id, ShopName = shop.ShopName });
    }

    [HttpPost]
    [Authorize(Roles = "Seller")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] ShopItemCreateDto dto)
    {
        if (!ModelState.IsValid) return Json(new { ok = false, error = "Invalid item data." });

        var userId = GetCurrentUserId();
        if (userId is null) return Json(new { ok = false, error = "Not authenticated." });

        var shop = await _shopRepo.GetByIdAsync(dto.ShopId);
        if (shop is null || shop.UserId != userId.Value)
            return Json(new { ok = false, error = "Permission denied." });

        if (!dto.Variants.Any() || dto.Variants.Any(v => !v.Skus.Any()))
            return Json(new { ok = false, error = "Each variant needs at least one SKU." });

        var lowestPrice = dto.Variants.SelectMany(v => v.Skus).Min(s => s.Price);

        var item = new Item
        {
            Title = dto.Title.Trim(),
            Description = dto.Description.Trim(),
            Category = dto.Category,
            Condition = dto.Condition,
            Price = lowestPrice,
            UserId = userId.Value,
            ShopId = shop.Id,
            ListingType = ListingType.Standard,
            Status = ItemStatus.Available,
            ImageUrls = new List<string>(),
            CreatedAt = DateTime.UtcNow,
            Variants = dto.Variants.Select(v => new ItemVariant
            {
                Name = v.Name.Trim(),
                Skus = v.Skus.Select(s => new ItemVariantSku
                {
                    Size = string.IsNullOrWhiteSpace(s.Size) ? null : s.Size.Trim(),
                    Price = s.Price,
                    Quantity = s.Quantity,
                    Status = SkuStatus.Available
                }).ToList()
            }).ToList()
        };

        await _itemRepo.AddAsync(item);
        _logger.LogInformation("Mobile: Seller {UserId} created shop item {ItemId}.", userId.Value, item.Id);

        return Json(new { ok = true, itemId = item.Id });
    }

    // POST /mobile/Shop/SaveListingImage
    [HttpPost]
    [Authorize(Roles = "Seller")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(ItemConstants.MaxImageSizeBytes + 1024)]
    public async Task<IActionResult> SaveListingImage(int itemId, int slot, IFormFile? file)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Json(new { ok = false, error = "Not authenticated." });

        var (fileErr, validatedFile) = ValidateImageFile(file);
        if (fileErr is not null) return Json(new { ok = false, error = fileErr });

        if (slot < 0 || slot >= ItemConstants.MaxImagesPerListing)
            return Json(new { ok = false, error = "Invalid image slot." });

        var item = await _itemRepo.GetByIdAsync(itemId);
        if (item is null || item.UserId != userId.Value)
            return Json(new { ok = false, error = "Permission denied." });

        var ext = Path.GetExtension(validatedFile!.FileName).ToLowerInvariant();
        var fileName = $"{slot}{ext}";
        var folder = Path.Combine(_env.WebRootPath, "uploads", "items", itemId.ToString());

        Directory.CreateDirectory(folder);
        var filePath = Path.Combine(folder, fileName);
        PurgeSlot(folder, slot.ToString(), filePath);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await validatedFile.CopyToAsync(stream);

        var url = $"/uploads/items/{itemId}/{fileName}";

        while (item.ImageUrls.Count <= slot) item.ImageUrls.Add(string.Empty);
        item.ImageUrls[slot] = url;
        while (item.ImageUrls.Count > 0 && string.IsNullOrEmpty(item.ImageUrls[^1]))
            item.ImageUrls.RemoveAt(item.ImageUrls.Count - 1);

        await _itemRepo.UpdateAsync(item);
        return Json(new { ok = true, url });
    }

    // POST /mobile/Shop/RemoveListingImage
    [HttpPost]
    [Authorize(Roles = "Seller")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveListingImage(int itemId, string url)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Json(new { ok = false, error = "Not authenticated." });

        var item = await _itemRepo.GetByIdAsync(itemId);
        if (item is null || item.UserId != userId.Value)
            return Json(new { ok = false, error = "Permission denied." });

        DeleteListingImageFile(url);
        item.ImageUrls.Remove(url);
        await _itemRepo.UpdateAsync(item);

        return Json(new { ok = true });
    }

    // GET /mobile/Shop/Edit/5
    [HttpGet]
    [Authorize(Roles = "Seller")]
    public async Task<IActionResult> Edit(int id)
    {
        var (item, err) = await GetOwnedShopItemTrackedAsync(id);
        if (err is not null) return err;

        var shop = await _shopRepo.GetByIdAsync(item!.ShopId!.Value);

        var vm = new ShopItemEditViewModel
        {
            ItemId = item.Id,
            ShopId = item.ShopId!.Value,
            ShopName = shop?.ShopName ?? string.Empty,
            Title = item.Title,
            Description = item.Description,
            Category = item.Category,
            Condition = item.Condition,
            ImageUrls = item.ImageUrls.ToList(),
            Variants = item.Variants.Select(v => new ShopItemVariantViewModel
            {
                VariantId = v.Id,
                Name = v.Name,
                Skus = v.Skus.Select(s => new ShopItemSkuViewModel
                {
                    SkuId = s.Id,
                    Size = s.Size,
                    Price = s.Price,
                    Quantity = s.Quantity,
                    IsAvailable = s.Status == SkuStatus.Available
                }).ToList()
            }).ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [Authorize(Roles = "Seller")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([FromBody] ShopItemEditDto dto)
    {
        if (!ModelState.IsValid) return Json(new { ok = false, error = "Invalid data." });

        var userId = GetCurrentUserId();
        if (userId is null) return Json(new { ok = false, error = "Not authenticated." });

        if (!dto.Variants.Any() || dto.Variants.Any(v => !v.Skus.Any()))
            return Json(new { ok = false, error = "Each variant needs at least one SKU." });

        var item = await _context.Items
            .Include(i => i.Variants).ThenInclude(v => v.Skus)
            .FirstOrDefaultAsync(i => i.Id == dto.ShopItemId);

        if (item is null || item.ShopId is null || item.UserId != userId.Value)
            return Json(new { ok = false, error = "Permission denied." });

        item.Title = dto.Title.Trim();
        item.Description = dto.Description.Trim();
        item.Category = dto.Category;
        item.Condition = dto.Condition;

        var incomingVariantIds = dto.Variants.Where(v => v.VariantId.HasValue).Select(v => v.VariantId!.Value).ToHashSet();

        foreach (var existing in item.Variants.ToList())
        {
            if (incomingVariantIds.Contains(existing.Id)) continue;
            var skuIds = existing.Skus.Select(s => s.Id).ToList();
            bool hasOrders = await _context.Orders.AnyAsync(o => o.ItemVariantSkuId != null && skuIds.Contains(o.ItemVariantSkuId.Value));
            if (hasOrders)
            {
                foreach (var sku in existing.Skus) { sku.Quantity = 0; sku.Status = SkuStatus.Sold; }
            }
            else
            {
                item.Variants.Remove(existing);
            }
        }

        foreach (var vDto in dto.Variants)
        {
            ItemVariant variant;
            if (vDto.VariantId.HasValue)
            {
                variant = item.Variants.First(v => v.Id == vDto.VariantId.Value);
                variant.Name = vDto.Name.Trim();
            }
            else
            {
                variant = new ItemVariant { Name = vDto.Name.Trim(), Skus = new List<ItemVariantSku>() };
                item.Variants.Add(variant);
            }

            var incomingSkuIds = vDto.Skus.Where(s => s.SkuId.HasValue).Select(s => s.SkuId!.Value).ToHashSet();

            foreach (var existingSku in variant.Skus.ToList())
            {
                if (incomingSkuIds.Contains(existingSku.Id)) continue;
                bool hasOrders = await _context.Orders.AnyAsync(o => o.ItemVariantSkuId == existingSku.Id);
                if (hasOrders) { existingSku.Quantity = 0; existingSku.Status = SkuStatus.Sold; }
                else variant.Skus.Remove(existingSku);
            }

            foreach (var sDto in vDto.Skus)
            {
                if (sDto.SkuId.HasValue)
                {
                    var sku = variant.Skus.First(s => s.Id == sDto.SkuId.Value);
                    sku.Size = string.IsNullOrWhiteSpace(sDto.Size) ? null : sDto.Size.Trim();
                    sku.Price = sDto.Price;
                    sku.Quantity = sDto.Quantity;
                    sku.Status = sDto.Quantity > 0 ? SkuStatus.Available : SkuStatus.Sold;
                }
                else
                {
                    variant.Skus.Add(new ItemVariantSku
                    {
                        Size = string.IsNullOrWhiteSpace(sDto.Size) ? null : sDto.Size.Trim(),
                        Price = sDto.Price,
                        Quantity = sDto.Quantity,
                        Status = SkuStatus.Available
                    });
                }
            }
        }

        var allSkus = item.Variants.SelectMany(v => v.Skus).ToList();
        if (allSkus.Any(s => s.Status == SkuStatus.Available))
            item.Price = allSkus.Where(s => s.Status == SkuStatus.Available).Min(s => s.Price);

        await _context.SaveChangesAsync();
        _logger.LogInformation("Mobile: Seller {UserId} updated shop item {ItemId}.", userId.Value, item.Id);

        return Json(new { ok = true });
    }

    // GET /mobile/Shop/Delete/5
    [HttpGet]
    [Authorize(Roles = "Seller")]
    public async Task<IActionResult> Delete(int id)
    {
        var (item, err) = await GetOwnedShopItemTrackedAsync(id);
        if (err is not null) return err;
        return View(item);
    }

    [HttpPost, ActionName("Delete")]
    [Authorize(Roles = "Seller")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var (item, err) = await GetOwnedShopItemTrackedAsync(id);
        if (err is not null) return err;

        bool hasOrders = await _context.Orders.AnyAsync(o => o.ItemId == id);
        if (hasOrders)
        {
            TempData["ErrorMessage"] = $"'{item!.Title}' cannot be deleted because it has associated orders.";
            return RedirectToAction(nameof(Mine));
        }

        foreach (var url in item!.ImageUrls) DeleteListingImageFile(url);
        await _itemRepo.DeleteAsync(id);

        _logger.LogInformation("Mobile: Seller {UserId} deleted shop item {ItemId}.", item.UserId, id);
        TempData["SuccessMessage"] = $"'{item.Title}' was deleted.";
        return RedirectToAction(nameof(Mine));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<int> GetSoldCountAsync(int itemId)
    {
        var skuIds = await _context.ItemVariantSkus
            .Where(s => s.Variant != null && s.Variant.ItemId == itemId)
            .Select(s => s.Id).ToListAsync();
        if (!skuIds.Any()) return 0;

        return await _context.OrderItems
            .Where(oi => skuIds.Contains(oi.ItemVariantSkuId))
            .Join(_context.Orders, oi => oi.OrderId, o => o.Id, (oi, o) => new { oi, o })
            .Where(x => x.o.Status == OrderStatus.Completed)
            .SumAsync(x => x.oi.Quantity);
    }

    private async Task<(Item? item, IActionResult? result)> GetOwnedShopItemTrackedAsync(int id)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return (null, Unauthorized());

        var item = await _itemRepo.GetByIdWithVariantsTrackedAsync(id);
        if (item is null || item.ShopId is null) return (null, NotFound());
        if (item.UserId != userId.Value) return (null, Forbid());

        return (item, null);
    }

    private static (string? error, IFormFile? file) ValidateImageFile(IFormFile? file)
    {
        if (file is null || file.Length == 0) return ("No file received.", null);
        if (file.Length > ItemConstants.MaxImageSizeBytes) return ("File exceeds the 5 MB limit.", null);
        if (!_allowedMimeTypes.Contains(file.ContentType)) return ("Only JPEG, PNG, WebP, and GIF images are allowed.", null);
        return (null, file);
    }

    private static void PurgeSlot(string folder, string baseName, string keepPath)
    {
        if (!Directory.Exists(folder)) return;
        foreach (var old in Directory.GetFiles(folder, $"{baseName}.*"))
        {
            if (!old.Equals(keepPath, StringComparison.OrdinalIgnoreCase))
            {
                try { System.IO.File.Delete(old); }
                catch { /* ignore */ }
            }
        }
    }

    private void DeleteListingImageFile(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        var segments = url.TrimStart('/').Split('/');
        if (segments.Length < 3) return;
        var fullPath = Path.Combine(_env.WebRootPath, Path.Combine(segments));
        if (System.IO.File.Exists(fullPath))
        {
            try { System.IO.File.Delete(fullPath); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete image: {Path}", fullPath); }
        }
    }
}