using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ThriftLoop.Constants;
using ThriftLoop.Data;
using ThriftLoop.Enums;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Implementation;
using ThriftLoop.Repositories.Interface;
using ThriftLoop.ViewModels;

namespace ThriftLoop.Controllers;

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

    // ══════════════════════════════════════════════════════════════════════
    //  SHOP PROFILE
    // ══════════════════════════════════════════════════════════════════════

    // GET /Shop/Index/5
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Index(int id)
    {
        var shop = await _shopRepo.GetByIdAsync(id);
        if (shop is null) return NotFound();

        var currentUserId = GetCurrentUserId();
        var isOwner = currentUserId.HasValue && shop.UserId == currentUserId.Value;

        var items = await _itemRepo.GetByShopIdAsync(id);

        // Filter out disabled items for non-owners
        if (!isOwner)
        {
            items = items.Where(i => i.Status != ItemStatus.Disabled).ToList();
        }

        // Calculate sold counts for each item
        var soldCounts = new Dictionary<int, int>();
        foreach (var item in items)
        {
            soldCounts[item.Id] = await GetSoldCountAsync(item.Id);
        }

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

        return View(vm);
    }

    // GET /Shop/Mine
    [HttpGet]
    [Authorize(Roles = "Seller")]
    public async Task<IActionResult> Mine()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return RedirectToAction("Login", "Auth");

        var shop = await _shopRepo.GetByUserIdAsync(userId.Value);
        if (shop is null)
        {
            _logger.LogWarning("Seller {UserId} has no SellerProfile row.", userId.Value);
            TempData["ShopError"] = "Your shop profile could not be found. Please contact support.";
            return RedirectToAction("Index", "Home");
        }

        return RedirectToAction(nameof(Index), new { id = shop.Id });
    }

    // POST /Shop/ToggleItemStatus  (AJAX — enable/disable listing)
    [HttpPost]
    [Authorize(Roles = "Seller")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleItemStatus([FromBody] ToggleItemStatusDto dto)
    {
        if (!ModelState.IsValid)
            return Json(new { ok = false, error = "Invalid request." });

        var userId = GetCurrentUserId();
        if (userId is null)
            return Json(new { ok = false, error = "Not authenticated." });

        var item = await _context.Items
            .Include(i => i.Variants)
            .FirstOrDefaultAsync(i => i.Id == dto.ItemId);

        if (item is null || item.ShopId is null)
            return Json(new { ok = false, error = "Item not found." });

        // Verify ownership through the shop
        var shop = await _shopRepo.GetByIdAsync(item.ShopId.Value);
        if (shop is null || shop.UserId != userId.Value)
            return Json(new { ok = false, error = "Permission denied." });

        // Toggle between Available and Disabled
        if (item.Status != ItemStatus.Available && item.Status != ItemStatus.Disabled)
        {
            return Json(new { ok = false, error = $"Cannot {(item.Status == ItemStatus.Disabled ? "enable" : "disable")} this listing because it is {item.Status.ToString().ToLower()}." });
        }

        var newStatus = item.Status == ItemStatus.Available
            ? ItemStatus.Disabled
            : ItemStatus.Available;

        item.Status = newStatus;
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Seller {UserId} {Action} shop item {ItemId}.",
            userId.Value,
            newStatus == ItemStatus.Disabled ? "disabled" : "enabled",
            item.Id);

        return Json(new
        {
            ok = true,
            isDisabled = newStatus == ItemStatus.Disabled,
            message = newStatus == ItemStatus.Disabled
                ? "Listing is now hidden from public view."
                : "Listing is now visible to everyone."
        });
    }

    // ══════════════════════════════════════════════════════════════════════
    //  SHOP DISCOUNT ENDPOINTS
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Applies a discount to a shop item. Available immediately.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Seller")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyShopDiscount([FromBody] ApplyDiscountDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Json(new DiscountResponse { Success = false, Error = "Not authenticated." });

        var item = await _context.Items
            .Include(i => i.Variants)
                .ThenInclude(v => v.Skus)
            .FirstOrDefaultAsync(i => i.Id == dto.ItemId);

        if (item is null)
            return Json(new DiscountResponse { Success = false, Error = "Item not found." });

        // Verify ownership through shop
        if (item.ShopId is null)
            return Json(new DiscountResponse { Success = false, Error = "This endpoint is for shop items only." });

        var shop = await _shopRepo.GetByIdAsync(item.ShopId.Value);
        if (shop is null || shop.UserId != userId.Value)
            return Json(new DiscountResponse { Success = false, Error = "Permission denied." });

        // Check if item is available or disabled (can discount both)
        if (item.Status != ItemStatus.Available && item.Status != ItemStatus.Disabled)
            return Json(new DiscountResponse { Success = false, Error = "Only available or disabled items can be discounted." });

        // Validate discount percentage
        if (dto.DiscountPercentage < 1 || dto.DiscountPercentage > 99)
            return Json(new DiscountResponse { Success = false, Error = "Discount must be between 1% and 99%." });

        // Validate expiration (if provided, must be in the future)
        if (dto.ExpiresAt.HasValue && dto.ExpiresAt.Value <= DateTime.UtcNow)
            return Json(new DiscountResponse { Success = false, Error = "Expiration date must be in the future." });

        // Store original price if not already discounted
        if (!item.OriginalPrice.HasValue)
            item.OriginalPrice = item.Price;

        // Calculate new base price
        var discountMultiplier = (100 - dto.DiscountPercentage) / 100m;
        var newBasePrice = Math.Round(item.OriginalPrice.Value * discountMultiplier, 2);

        // Apply discount to item
        item.Price = newBasePrice;
        item.DiscountPercentage = dto.DiscountPercentage;
        item.DiscountedAt = DateTime.UtcNow;
        item.DiscountExpiresAt = dto.ExpiresAt;

        // FIX: Apply discount to all SKUs using OriginalPrice, not current price
        int updatedSkuCount = 0;
        foreach (var variant in item.Variants)
        {
            foreach (var sku in variant.Skus)
            {
                // Store original SKU price if not already stored
                if (!sku.OriginalPrice.HasValue)
                    sku.OriginalPrice = sku.Price;

                // Apply discount based on ORIGINAL price, not current price
                sku.Price = Math.Round(sku.OriginalPrice.Value * discountMultiplier, 2);
                sku.DiscountPercentage = dto.DiscountPercentage;
                updatedSkuCount++;
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Seller {UserId} applied {DiscountPercent}% discount to Shop Item {ItemId}. " +
            "Updated {SkuCount} SKUs. New base price: ₱{NewPrice}. Expires: {Expires}",
            userId, dto.DiscountPercentage, item.Id, updatedSkuCount, newBasePrice,
            dto.ExpiresAt?.ToString("O") ?? "never");

        return Json(new DiscountResponse
        {
            Success = true,
            NewPrice = newBasePrice,
            OriginalPrice = item.OriginalPrice.Value,
            DiscountPercentage = dto.DiscountPercentage,
            SavingsAmount = item.OriginalPrice.Value - newBasePrice,
            ExpiresAt = dto.ExpiresAt?.ToString("MMM d, yyyy 'at' h:mm tt"),
            IsIndefinite = !dto.ExpiresAt.HasValue
        });
    }

    /// <summary>
    /// Removes the discount from a shop item, restoring original prices.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Seller")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveShopDiscount(int itemId)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Json(new { success = false, error = "Not authenticated." });

        var item = await _context.Items
            .Include(i => i.Variants)
                .ThenInclude(v => v.Skus)
            .FirstOrDefaultAsync(i => i.Id == itemId);

        if (item is null)
            return Json(new { success = false, error = "Item not found." });

        // Verify ownership through shop
        if (item.ShopId is null)
            return Json(new { success = false, error = "This endpoint is for shop items only." });

        var shop = await _shopRepo.GetByIdAsync(item.ShopId.Value);
        if (shop is null || shop.UserId != userId.Value)
            return Json(new { success = false, error = "Permission denied." });

        // Check if there's a discount to remove
        if (!item.OriginalPrice.HasValue)
            return Json(new { success = false, error = "This item is not discounted." });

        // Restore item price
        var restoredPrice = item.OriginalPrice.Value;
        item.Price = restoredPrice;
        item.OriginalPrice = null;
        item.DiscountPercentage = null;
        item.DiscountedAt = null;
        item.DiscountExpiresAt = null;

        // FIX: Restore each SKU using its own OriginalPrice
        int restoredSkuCount = 0;
        foreach (var variant in item.Variants)
        {
            foreach (var sku in variant.Skus)
            {
                if (sku.OriginalPrice.HasValue)
                {
                    sku.Price = sku.OriginalPrice.Value;
                    sku.OriginalPrice = null;
                    sku.DiscountPercentage = null;
                    restoredSkuCount++;
                }
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Seller {UserId} removed discount from Shop Item {ItemId}. " +
            "Restored {SkuCount} SKUs to their original prices. Base price restored to ₱{Price}.",
            userId, item.Id, restoredSkuCount, restoredPrice);

        return Json(new
        {
            success = true,
            restoredPrice = restoredPrice,
            message = "Discount removed successfully."
        });
    }

    /// <summary>
    /// Bulk apply discount to multiple shop items.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Seller")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkApplyDiscount([FromBody] BulkDiscountDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Json(new { success = false, error = "Not authenticated." });

        if (dto.ItemIds == null || dto.ItemIds.Count == 0)
            return Json(new { success = false, error = "No items selected." });

        // Validate discount percentage
        if (dto.DiscountPercentage < 1 || dto.DiscountPercentage > 99)
            return Json(new { success = false, error = "Discount must be between 1% and 99%." });

        var successCount = 0;
        var errors = new List<string>();
        var discountMultiplier = (100 - dto.DiscountPercentage) / 100m;

        foreach (var itemId in dto.ItemIds)
        {
            var item = await _context.Items
                .Include(i => i.Variants)
                    .ThenInclude(v => v.Skus)
                .FirstOrDefaultAsync(i => i.Id == itemId);

            if (item == null || item.ShopId == null)
            {
                errors.Add($"Item {itemId} not found or not a shop item.");
                continue;
            }

            var shop = await _shopRepo.GetByIdAsync(item.ShopId.Value);
            if (shop == null || shop.UserId != userId.Value)
            {
                errors.Add($"Permission denied for item {itemId}.");
                continue;
            }

            if (item.Status != ItemStatus.Available && item.Status != ItemStatus.Disabled)
            {
                errors.Add($"Item {itemId} cannot be discounted (status: {item.Status}).");
                continue;
            }

            // Store original price if not already discounted
            if (!item.OriginalPrice.HasValue)
                item.OriginalPrice = item.Price;

            var newBasePrice = Math.Round(item.OriginalPrice.Value * discountMultiplier, 2);

            item.Price = newBasePrice;
            item.DiscountPercentage = dto.DiscountPercentage;
            item.DiscountedAt = DateTime.UtcNow;
            item.DiscountExpiresAt = dto.ExpiresAt;

            // FIX: Apply discount to SKUs using OriginalPrice
            foreach (var variant in item.Variants)
            {
                foreach (var sku in variant.Skus)
                {
                    // Store original SKU price if not already stored
                    if (!sku.OriginalPrice.HasValue)
                        sku.OriginalPrice = sku.Price;

                    // Apply discount based on ORIGINAL price
                    sku.Price = Math.Round(sku.OriginalPrice.Value * discountMultiplier, 2);
                    sku.DiscountPercentage = dto.DiscountPercentage;
                }
            }

            successCount++;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Seller {UserId} bulk applied {DiscountPercent}% discount to {Count} items.",
            userId, dto.DiscountPercentage, successCount);

        return Json(new
        {
            success = true,
            successCount,
            errorCount = errors.Count,
            errors,
            message = $"Discount applied to {successCount} item{(successCount != 1 ? "s" : "")}."
        });
    }

    // POST /Shop/SaveField  (AJAX — shop name/bio)
    [HttpPost]
    [Authorize(Roles = "Seller")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveField([FromBody] SaveFieldDto dto)
    {
        if (!ModelState.IsValid)
            return Json(new { ok = false, error = "Invalid request." });

        var userId = GetCurrentUserId();
        if (userId is null)
            return Json(new { ok = false, error = "Not authenticated." });

        var shop = await _shopRepo.GetByIdAsync(dto.ShopId);
        if (shop is null || shop.UserId != userId.Value)
            return Json(new { ok = false, error = "Permission denied." });

        var value = dto.Value?.Trim();

        switch (dto.Field)
        {
            case "ShopName":
                if (string.IsNullOrWhiteSpace(value))
                    return Json(new { ok = false, error = "Shop name cannot be empty." });
                if (value.Length > 100)
                    return Json(new { ok = false, error = "Shop name cannot exceed 100 characters." });
                shop.ShopName = value;
                break;

            case "Bio":
                if (value?.Length > 500)
                    return Json(new { ok = false, error = "Bio cannot exceed 500 characters." });
                shop.Bio = string.IsNullOrWhiteSpace(value) ? null : value;
                break;

            case "StoreAddress":
                if (value?.Length > 1000)
                    return Json(new { ok = false, error = "Address is too long." });
                shop.StoreAddress = string.IsNullOrWhiteSpace(value) ? null : value;
                break;

            default:
                return Json(new { ok = false, error = "Unknown field." });
        }

        await _shopRepo.UpdateAsync(shop);
        return Json(new { ok = true });
    }

    // POST /Shop/SaveLocation (AJAX — save shop coordinates)
    [HttpPost]
    [Authorize(Roles = "Seller")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveLocation([FromBody] SaveLocationDto dto)
    {
        if (!ModelState.IsValid)
            return Json(new { ok = false, error = "Invalid request." });

        var userId = GetCurrentUserId();
        if (userId is null)
            return Json(new { ok = false, error = "Not authenticated." });

        var shop = await _shopRepo.GetByIdAsync(dto.ShopId);
        if (shop is null || shop.UserId != userId.Value)
            return Json(new { ok = false, error = "Permission denied." });

        shop.Latitude = dto.Latitude;
        shop.Longitude = dto.Longitude;
        if (!string.IsNullOrWhiteSpace(dto.Address))
            shop.StoreAddress = dto.Address.Trim();

        await _shopRepo.UpdateAsync(shop);
        return Json(new { ok = true });
    }

    // POST /Shop/SaveImage  (AJAX — shop banner/logo)
    [HttpPost]
    [Authorize(Roles = "Seller")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(ItemConstants.MaxImageSizeBytes + 1024)]
    public async Task<IActionResult> SaveImage(int shopId, string field, IFormFile? file)
    {
        _logger.LogInformation($"SaveImage called - ShopId: {shopId}, Field: {field}, File: {file?.FileName ?? "null"}");

        var userId = GetCurrentUserId();
        if (userId is null)
        {
            _logger.LogWarning("User not authenticated");
            return Json(new { ok = false, error = "Not authenticated." });
        }

        var (fileErr, validatedFile) = ValidateImageFile(file);
        if (fileErr is not null)
        {
            _logger.LogWarning($"File validation failed: {fileErr}");
            return Json(new { ok = false, error = fileErr });
        }

        if (field is not ("BannerUrl" or "LogoUrl"))
        {
            _logger.LogWarning($"Invalid field: {field}");
            return Json(new { ok = false, error = "Unknown image field." });
        }

        var shop = await _shopRepo.GetByIdAsync(shopId);
        if (shop is null || shop.UserId != userId.Value)
        {
            _logger.LogWarning($"Permission denied - Shop exists: {shop != null}, User matches: {shop?.UserId == userId.Value}");
            return Json(new { ok = false, error = "Permission denied." });
        }

        var ext = Path.GetExtension(validatedFile!.FileName).ToLowerInvariant();
        var fileName = field == "BannerUrl" ? $"banner{ext}" : $"logo{ext}";
        var folder = Path.Combine(_env.WebRootPath, "uploads", "shops", shopId.ToString());

        _logger.LogInformation($"Saving to folder: {folder}, fileName: {fileName}");

        try
        {
            Directory.CreateDirectory(folder);
            var filePath = Path.Combine(folder, fileName);

            // Delete old files with same base name but different extension
            PurgeSlot(folder, Path.GetFileNameWithoutExtension(fileName), filePath);

            // Save the new file
            await using (var stream = new FileStream(filePath, FileMode.Create))
                await validatedFile.CopyToAsync(stream);

            var url = $"/uploads/shops/{shopId}/{fileName}";
            _logger.LogInformation($"Generated URL: {url}");

            // Update the shop entity
            if (field == "BannerUrl")
            {
                shop.BannerUrl = url;
                _logger.LogInformation($"Updated shop.BannerUrl to: {url}");
            }
            else
            {
                shop.LogoUrl = url;
                _logger.LogInformation($"Updated shop.LogoUrl to: {url}");
            }

            // Save changes - now the entity is tracked because we removed AsNoTracking()
            await _shopRepo.UpdateAsync(shop);
            _logger.LogInformation($"Shop updated successfully");

            return Json(new { ok = true, url });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving image for shop {ShopId}", shopId);
            return Json(new { ok = false, error = "Failed to save image. Please try again." });
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  LISTING CRUD
    // ══════════════════════════════════════════════════════════════════════

    // GET /Shop/Details/5
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var item = await _itemRepo.GetByIdWithVariantsAsync(id);
        if (item == null || item.ShopId == null)
            return NotFound();

        var shop = await _shopRepo.GetByIdAsync(item.ShopId.Value);
        if (shop == null)
            return NotFound();

        var currentUserId = User.Identity?.IsAuthenticated == true
            ? int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)
            : (int?)null;

        // Get all available SKUs
        var allSkus = item.Variants
            .SelectMany(v => v.Skus)
            .Where(s => s.Status == ThriftLoop.Enums.SkuStatus.Available)
            .ToList();

        // Calculate starting price (lowest current price)
        var startingPrice = allSkus.Any()
            ? allSkus.Min(s => s.Price)
            : item.Price;

        // Calculate original starting price (lowest original price) for discount display
        decimal? originalStartingPrice = null;
        if (item.HasActiveDiscount)
        {
            // Get SKUs that have OriginalPrice set
            var skusWithOriginalPrice = allSkus.Where(s => s.OriginalPrice.HasValue).ToList();

            if (skusWithOriginalPrice.Any())
            {
                // Get the lowest original price among available SKUs that have it set
                originalStartingPrice = skusWithOriginalPrice.Min(s => s.OriginalPrice!.Value);
            }
            else
            {
                // Fall back to item.OriginalPrice
                originalStartingPrice = item.OriginalPrice;
            }
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
                    IsAvailable = s.Status == ThriftLoop.Enums.SkuStatus.Available && s.Quantity > 0,
                    OriginalPrice = s.OriginalPrice
                }).ToList()
            }).ToList()
        };

        return View(viewModel);
    }

    // GET /Shop/Create
    [HttpGet]
    [Authorize(Roles = "Seller")]
    public async Task<IActionResult> Create()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return RedirectToAction("Login", "Auth");

        var shop = await _shopRepo.GetByUserIdAsync(userId.Value);
        if (shop is null) return RedirectToAction("Index", "Home");

        return View(new ShopItemCreateViewModel { ShopId = shop.Id, ShopName = shop.ShopName });
    }

    // POST /Shop/Create  (AJAX — creates item, images uploaded separately)
    [HttpPost]
    [Authorize(Roles = "Seller")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] ShopItemCreateDto dto)
    {
        if (!ModelState.IsValid)
            return Json(new { ok = false, error = "Invalid item data." });

        var userId = GetCurrentUserId();
        if (userId is null) return Json(new { ok = false, error = "Not authenticated." });

        var shop = await _shopRepo.GetByIdAsync(dto.ShopId);
        if (shop is null || shop.UserId != userId.Value)
            return Json(new { ok = false, error = "Permission denied." });

        if (!dto.Variants.Any())
            return Json(new { ok = false, error = "At least one variant is required." });

        if (dto.Variants.Any(v => !v.Skus.Any()))
            return Json(new { ok = false, error = "Each variant must have at least one SKU." });

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
        _logger.LogInformation("Seller {UserId} created shop item {ItemId}.", userId.Value, item.Id);

        return Json(new { ok = true, itemId = item.Id });
    }

    // POST /Shop/SaveListingImage  (AJAX — one image per call, slot 0-4)
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

        await using (var s = new FileStream(filePath, FileMode.Create))
            await validatedFile.CopyToAsync(s);

        var url = $"/uploads/items/{itemId}/{fileName}";

        while (item.ImageUrls.Count <= slot) item.ImageUrls.Add(string.Empty);
        item.ImageUrls[slot] = url;
        while (item.ImageUrls.Count > 0 && string.IsNullOrEmpty(item.ImageUrls[^1]))
            item.ImageUrls.RemoveAt(item.ImageUrls.Count - 1);

        await _itemRepo.UpdateAsync(item);
        return Json(new { ok = true, url });
    }

    // POST /Shop/RemoveListingImage  (AJAX — remove one image by URL)
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

    // GET /Shop/Edit/5
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

    // POST /Shop/Edit  (AJAX — updates text + variant tree)
    [HttpPost]
    [Authorize(Roles = "Seller")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([FromBody] ShopItemEditDto dto)
    {
        if (!ModelState.IsValid)
            return Json(new { ok = false, error = "Invalid data." });

        var userId = GetCurrentUserId();
        if (userId is null) return Json(new { ok = false, error = "Not authenticated." });

        if (!dto.Variants.Any())
            return Json(new { ok = false, error = "At least one variant is required." });

        if (dto.Variants.Any(v => !v.Skus.Any()))
            return Json(new { ok = false, error = "Each variant must have at least one SKU." });

        // Load tracked so EF can detect changes
        var item = await _context.Items
            .Include(i => i.Variants)
                .ThenInclude(v => v.Skus)
            .FirstOrDefaultAsync(i => i.Id == dto.ShopItemId);

        if (item is null || item.ShopId is null || item.UserId != userId.Value)
            return Json(new { ok = false, error = "Permission denied." });

        // ── Update scalar fields ──────────────────────────────────────────
        item.Title = dto.Title.Trim();
        item.Description = dto.Description.Trim();
        item.Category = dto.Category;
        item.Condition = dto.Condition;

        // ── Variant / SKU merge ───────────────────────────────────────────

        var incomingVariantIds = dto.Variants
            .Where(v => v.VariantId.HasValue)
            .Select(v => v.VariantId!.Value)
            .ToHashSet();

        // Remove variants not in the DTO
        foreach (var existing in item.Variants.ToList())
        {
            if (incomingVariantIds.Contains(existing.Id)) continue;

            // Check if any SKU in this variant has been ordered
            var skuIds = existing.Skus.Select(s => s.Id).ToList();
            bool hasOrders = await _context.Orders
                .AnyAsync(o => o.ItemVariantSkuId != null && skuIds.Contains(o.ItemVariantSkuId.Value));

            if (hasOrders)
            {
                // Soft-remove: zero out stock rather than hard delete
                foreach (var sku in existing.Skus)
                {
                    sku.Quantity = 0;
                    sku.Status = SkuStatus.Sold;
                }
            }
            else
            {
                item.Variants.Remove(existing);
            }
        }

        // Update existing variants / add new ones
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

            var incomingSkuIds = vDto.Skus
                .Where(s => s.SkuId.HasValue)
                .Select(s => s.SkuId!.Value)
                .ToHashSet();

            // Remove SKUs not in DTO
            foreach (var existingSku in variant.Skus.ToList())
            {
                if (incomingSkuIds.Contains(existingSku.Id)) continue;

                bool hasOrders = await _context.Orders
                    .AnyAsync(o => o.ItemVariantSkuId == existingSku.Id);

                if (hasOrders)
                {
                    existingSku.Quantity = 0;
                    existingSku.Status = SkuStatus.Sold;
                }
                else
                {
                    variant.Skus.Remove(existingSku);
                }
            }

            // Update / add SKUs
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

        // Keep Item.Price in sync with lowest available SKU price
        var allSkus = item.Variants.SelectMany(v => v.Skus).ToList();
        if (allSkus.Any(s => s.Status == SkuStatus.Available))
            item.Price = allSkus.Where(s => s.Status == SkuStatus.Available).Min(s => s.Price);

        await _context.SaveChangesAsync();
        _logger.LogInformation("Seller {UserId} updated shop item {ItemId}.", userId.Value, item.Id);

        return Json(new { ok = true });
    }

    // GET /Shop/Delete/5
    [HttpGet]
    [Authorize(Roles = "Seller")]
    public async Task<IActionResult> Delete(int id)
    {
        var (item, err) = await GetOwnedShopItemTrackedAsync(id);
        if (err is not null) return err;
        return View(item);
    }

    // POST /Shop/Delete/5
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
            TempData["ErrorMessage"] =
                $"'{item!.Title}' cannot be deleted because it has associated orders. " +
                "Contact support if you need this listing removed.";
            return RedirectToAction(nameof(Mine));
        }

        foreach (var url in item!.ImageUrls)
            DeleteListingImageFile(url);

        await _itemRepo.DeleteAsync(id);
        _logger.LogInformation("Seller {UserId} deleted shop item {ItemId}.", item.UserId, id);

        TempData["SuccessMessage"] = $"'{item.Title}' was deleted.";
        return RedirectToAction(nameof(Mine));
    }

    // ══════════════════════════════════════════════════════════════════════
    //  PRIVATE HELPERS
    // ══════════════════════════════════════════════════════════════════════

    private int? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var id) ? id : null;
    }

    private async Task<int> GetSoldCountAsync(int itemId)
    {
        // Get all SKU IDs for this item
        var skuIds = await _context.ItemVariantSkus
            .Where(s => s.Variant != null && s.Variant.ItemId == itemId)
            .Select(s => s.Id)
            .ToListAsync();

        if (!skuIds.Any()) return 0;

        // Count completed orders for these SKUs
        return await _context.OrderItems
            .Where(oi => skuIds.Contains(oi.ItemVariantSkuId))
            .Join(_context.Orders,
                oi => oi.OrderId,
                o => o.Id,
                (oi, o) => new { oi, o })
            .Where(x => x.o.Status == OrderStatus.Completed)
            .SumAsync(x => x.oi.Quantity);
    }

    /// <summary>
    /// Loads the item with variants+SKUs (no-tracking) and verifies it is a shop item owned
    /// by the current user. Returns Forbid/NotFound result on failure.
    /// Used by Details view (public).
    /// </summary>
    private async Task<(Item? item, IActionResult? result)> GetOwnedShopItemAsync(int id)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return (null, Unauthorized());

        var item = await _itemRepo.GetByIdWithVariantsAsync(id);
        if (item is null || item.ShopId is null) return (null, NotFound());
        if (item.UserId != userId.Value) return (null, Forbid());

        return (item, null);
    }

    /// <summary>
    /// Loads the item with variants+SKUs (tracked) and verifies it is a shop item owned
    /// by the current user. Returns Forbid/NotFound result on failure.
    /// Used by Edit and Delete actions (requires tracking for updates).
    /// </summary>
    private async Task<(Item? item, IActionResult? result)> GetOwnedShopItemTrackedAsync(int id)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return (null, Unauthorized());

        var item = await _itemRepo.GetByIdWithVariantsTrackedAsync(id);
        if (item is null || item.ShopId is null) return (null, NotFound());
        if (item.UserId != userId.Value) return (null, Forbid());

        return (item, null);
    }

    /// <summary>Validates an uploaded image file. Returns (errorMessage, null) or (null, file).</summary>
    private static (string? error, IFormFile? file) ValidateImageFile(IFormFile? file)
    {
        if (file is null || file.Length == 0) return ("No file received.", null);
        if (file.Length > ItemConstants.MaxImageSizeBytes) return ("File exceeds the 5 MB limit.", null);
        if (!_allowedMimeTypes.Contains(file.ContentType))
            return ("Only JPEG, PNG, WebP, and GIF images are allowed.", null);
        return (null, file);
    }

    /// <summary>
    /// Deletes any existing file in the same folder with the same base name
    /// but a different extension (e.g. slot 0 switching from .jpg to .png).
    /// </summary>
    private static void PurgeSlot(string folder, string baseName, string keepPath)
    {
        if (!Directory.Exists(folder)) return;

        foreach (var old in Directory.GetFiles(folder, $"{baseName}.*"))
        {
            if (!old.Equals(keepPath, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    System.IO.File.Delete(old);
                }
                catch (Exception ex)
                {
                    // Log but don't throw - we can continue with the new file
                    Console.WriteLine($"Failed to delete old file {old}: {ex.Message}");
                }
            }
        }
    }

    private void DeleteListingImageFile(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        // URL format: /uploads/items/{itemId}/{fileName}
        var segments = url.TrimStart('/').Split('/');
        if (segments.Length < 3) return;
        var fullPath = Path.Combine(_env.WebRootPath, Path.Combine(segments));
        if (System.IO.File.Exists(fullPath))
        {
            try
            {
                System.IO.File.Delete(fullPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete image file: {Path}", fullPath);
            }
        }
    }
}

// DTOs
public class ToggleItemStatusDto
{
    public int ItemId { get; set; }
}

public class BulkDiscountDto
{
    public List<int> ItemIds { get; set; } = new();
    public decimal DiscountPercentage { get; set; }
    public DateTime? ExpiresAt { get; set; }
}