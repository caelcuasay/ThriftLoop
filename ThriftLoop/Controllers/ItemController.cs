using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ThriftLoop.Data;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;
using ThriftLoop.ViewModels;
using ThriftLoop.Enums;

namespace ThriftLoop.Controllers;

[Authorize]
public class ItemsController : Controller
{
    private readonly IItemRepository _itemRepository;
    private readonly ILogger<ItemsController> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly ApplicationDbContext _context;

    public ItemsController(
        IItemRepository itemRepository,
        IWebHostEnvironment env,
        ILogger<ItemsController> logger,
        ApplicationDbContext context)
    {
        _itemRepository = itemRepository;
        _env = env;
        _logger = logger;
        _context = context;
    }

    // ── Helper to check if current user is a rider ───────────────────────────
    private bool IsRider()
    {
        return User.HasClaim(c => c.Type == "IsRider" && c.Value == "true");
    }

    // ── Helper to check if a user has a complete profile ─────────────────────
    /// <summary>
    /// Returns true only when the user has both a non-empty PhoneNumber and Address.
    /// Required before the user can list or purchase items.
    /// </summary>
    private async Task<bool> HasCompleteProfileAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        return user is not null
            && !string.IsNullOrWhiteSpace(user.PhoneNumber)
            && !string.IsNullOrWhiteSpace(user.Address);
    }

    /// <summary>
    /// Redirects the user to their profile page with an explanatory message.
    /// </summary>
    private IActionResult RedirectToCompleteProfile(string action)
    {
        TempData["ProfileIncomplete"] =
            $"Please add your phone number and address before you can {action}.";
        return RedirectToAction("Index", "User");
    }

    // ── INDEX ─────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // Redirect riders to their dashboard
        if (IsRider())
        {
            _logger.LogWarning("Rider attempted to access Items/Index");
            return RedirectToAction("Index", "Rider");
        }

        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(rawId, out int userId)) return Unauthorized();

        var items = await _itemRepository.GetItemsByUserIdAsync(userId);
        _logger.LogInformation("User {UserId} viewed My Listings ({Count} items).", userId, items.Count);
        return View(items);
    }

    // ── DETAILS ───────────────────────────────────────────────────────────────

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Details(int id)
    {
        var item = await _itemRepository.GetByIdWithUserAsync(id);
        if (item is null) return NotFound();

        int? currentUserId = null;
        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(rawId, out int parsedId)) currentUserId = parsedId;

        ViewBag.IsOwner = currentUserId.HasValue && item.UserId == currentUserId.Value;
        ViewBag.IsAnonymous = !currentUserId.HasValue;
        ViewBag.CurrentUserId = currentUserId;
        ViewBag.IsCurrentWinner = currentUserId.HasValue && item.CurrentWinnerId == currentUserId.Value;
        ViewBag.IsOriginalGetter = currentUserId.HasValue && item.OriginalGetterUserId == currentUserId.Value;

        _logger.LogInformation("Item {ItemId} details viewed by user {UserId}.",
            item.Id, currentUserId?.ToString() ?? "anonymous");

        return View(item);
    }

    // ── BUY NOW ───────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> BuyNow(int id)
    {
        // Redirect riders to their dashboard
        if (IsRider())
        {
            _logger.LogWarning("Rider attempted to access Items/BuyNow");
            return RedirectToAction("Index", "Rider");
        }

        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(rawId, out int userId)) return Unauthorized();

        if (!await HasCompleteProfileAsync(userId))
            return RedirectToCompleteProfile("purchase items");

        return RedirectToAction("Checkout", "Orders", new { itemId = id });
    }

    // ── CREATE ────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        // Redirect riders to their dashboard
        if (IsRider())
        {
            _logger.LogWarning("Rider attempted to access Items/Create");
            return RedirectToAction("Index", "Rider");
        }

        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(rawId, out int userId)) return Unauthorized();

        if (!await HasCompleteProfileAsync(userId))
            return RedirectToCompleteProfile("list items for sale");

        return View(new ItemCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ItemCreateViewModel viewModel)
    {
        // Redirect riders to their dashboard
        if (IsRider())
        {
            _logger.LogWarning("Rider attempted to access Items/Create (POST)");
            return RedirectToAction("Index", "Rider");
        }

        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(rawId, out int userId)) return Unauthorized();

        if (!await HasCompleteProfileAsync(userId))
            return RedirectToCompleteProfile("list items for sale");

        if (!ModelState.IsValid) return View(viewModel);

        var imageUrls = new List<string>();

        if (viewModel.Images is { Count: > 0 })
        {
            foreach (var file in viewModel.Images.Take(5))
            {
                var (saved, error) = await SaveImageAsync(file);
                if (error is not null)
                {
                    ModelState.AddModelError(nameof(viewModel.Images), error);
                    foreach (var url in imageUrls) DeleteImageFile(url);
                    return View(viewModel);
                }
                if (saved is not null) imageUrls.Add(saved);
            }
        }

        var item = new Item
        {
            Title = viewModel.Title,
            Description = viewModel.Description,
            Price = viewModel.Price,
            Category = viewModel.Category,
            Condition = viewModel.Condition,
            Size = string.IsNullOrWhiteSpace(viewModel.Size) ? null : viewModel.Size,
            ImageUrls = imageUrls,
            CreatedAt = DateTime.UtcNow,
            UserId = userId,
            ListingType = viewModel.IsStealable ? ListingType.Stealable : ListingType.Standard,
            StealDurationHours = viewModel.IsStealable ? viewModel.StealDurationHours : null,
            StealEndsAt = null,
            CurrentWinnerId = null,
            OriginalGetterUserId = null,
            Status = ItemStatus.Available,
            // Fulfillment options from view model
            AllowDelivery = viewModel.AllowDelivery,
            AllowHalfway = viewModel.AllowHalfway,
            AllowPickup = viewModel.AllowPickup
        };

        await _itemRepository.AddAsync(item);
        _logger.LogInformation("User {UserId} created Item {ItemId} ({ListingType}). Fulfillment: Delivery={AllowDelivery}, Halfway={AllowHalfway}, Pickup={AllowPickup}.",
            userId, item.Id, item.ListingType, item.AllowDelivery, item.AllowHalfway, item.AllowPickup);

        TempData["SuccessMessage"] = $"'{item.Title}' was listed successfully!";
        return RedirectToAction(nameof(Index));
    }

    // ── EDIT ──────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        // Redirect riders to their dashboard
        if (IsRider())
        {
            _logger.LogWarning("Rider attempted to access Items/Edit");
            return RedirectToAction("Index", "Rider");
        }

        var (item, actionResult) = await GetOwnedItemAsync(id);
        if (actionResult is not null) return actionResult;
        return View(MapToEditViewModel(item!));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ItemEditViewModel viewModel)
    {
        // Redirect riders to their dashboard
        if (IsRider())
        {
            _logger.LogWarning("Rider attempted to access Items/Edit (POST)");
            return RedirectToAction("Index", "Rider");
        }

        if (id != viewModel.Id) return BadRequest();
        if (!ModelState.IsValid) return View(viewModel);

        var (item, actionResult) = await GetOwnedItemAsync(id);
        if (actionResult is not null) return actionResult;

        var imageUrls = item!.ImageUrls.ToList();

        foreach (var url in viewModel.RemovedImageUrls)
        {
            DeleteImageFile(url);
            imageUrls.Remove(url);
        }

        if (viewModel.NewImages is { Count: > 0 })
        {
            foreach (var file in viewModel.NewImages)
            {
                if (imageUrls.Count >= 5) break;

                var (saved, error) = await SaveImageAsync(file);
                if (error is not null)
                {
                    ModelState.AddModelError(nameof(viewModel.NewImages), error);
                    viewModel.ExistingImageUrls = imageUrls;
                    return View(viewModel);
                }
                if (saved is not null) imageUrls.Add(saved);
            }
        }

        item.Title = viewModel.Title;
        item.Description = viewModel.Description;
        item.Price = viewModel.Price;
        item.Category = viewModel.Category;
        item.Condition = viewModel.Condition;
        item.Size = string.IsNullOrWhiteSpace(viewModel.Size) ? null : viewModel.Size;
        item.ImageUrls = imageUrls;
        // Update fulfillment options
        item.AllowDelivery = viewModel.AllowDelivery;
        item.AllowHalfway = viewModel.AllowHalfway;
        item.AllowPickup = viewModel.AllowPickup;

        await _itemRepository.UpdateAsync(item);
        _logger.LogInformation("User {UserId} updated Item {ItemId}. Fulfillment: Delivery={AllowDelivery}, Halfway={AllowHalfway}, Pickup={AllowPickup}.",
            item.UserId, item.Id, item.AllowDelivery, item.AllowHalfway, item.AllowPickup);

        TempData["SuccessMessage"] = $"'{item.Title}' was updated successfully!";
        return RedirectToAction(nameof(Index));
    }

    // ── DELETE ────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        // Redirect riders to their dashboard
        if (IsRider())
        {
            _logger.LogWarning("Rider attempted to access Items/Delete");
            return RedirectToAction("Index", "Rider");
        }

        var (item, actionResult) = await GetOwnedItemAsync(id);
        if (actionResult is not null) return actionResult;
        return View(item);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        // Redirect riders to their dashboard
        if (IsRider())
        {
            _logger.LogWarning("Rider attempted to access Items/DeleteConfirmed");
            return RedirectToAction("Index", "Rider");
        }

        var (item, actionResult) = await GetOwnedItemAsync(id);
        if (actionResult is not null) return actionResult;

        // Check for associated orders
        bool hasOrders = await _context.Orders.AnyAsync(o => o.ItemId == id);
        if (hasOrders)
        {
            TempData["ErrorMessage"] =
                $"'{item!.Title}' cannot be deleted because it has associated orders. " +
                "Contact support if you need this listing removed.";
            return RedirectToAction(nameof(Index));
        }

        // Check for associated likes - and delete them first
        var itemLikes = await _context.ItemLikes
            .Where(il => il.ItemId == id)
            .ToListAsync();

        if (itemLikes.Any())
        {
            _context.ItemLikes.RemoveRange(itemLikes);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Deleted {Count} likes associated with Item {ItemId}.",
                itemLikes.Count, item!.Id);
        }

        // Also check for cart items that might reference this item
        var cartItems = await _context.CartItems
            .Where(ci => ci.ItemId == id)
            .ToListAsync();

        if (cartItems.Any())
        {
            _context.CartItems.RemoveRange(cartItems);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Deleted {Count} cart items associated with Item {ItemId}.",
                cartItems.Count, item!.Id);
        }

        // Check for order items that reference this item through SKUs
        // Get all SKU IDs for this item first
        var skuIds = await _context.ItemVariantSkus
            .Where(s => s.Variant != null && s.Variant.ItemId == id)
            .Select(s => s.Id)
            .ToListAsync();

        if (skuIds.Any())
        {
            var orderItems = await _context.OrderItems
                .Where(oi => skuIds.Contains(oi.ItemVariantSkuId))
                .ToListAsync();

            if (orderItems.Any())
            {
                TempData["ErrorMessage"] =
                    $"'{item!.Title}' cannot be deleted because it has associated order items. " +
                    "Contact support if you need this listing removed.";
                return RedirectToAction(nameof(Index));
            }
        }

        // Delete the image files from disk
        foreach (var url in item!.ImageUrls)
            DeleteImageFile(url);

        // Finally delete the item itself
        await _itemRepository.DeleteAsync(id);

        _logger.LogInformation("User {UserId} deleted Item {ItemId}.", item.UserId, item.Id);

        TempData["SuccessMessage"] = $"'{item.Title}' was deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ── DISCOUNT ENDPOINTS ────────────────────────────────────────────────────

    /// <summary>
    /// Applies a discount to a P2P item. Only available after 24 hours of listing.
    /// </summary>
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyDiscount([FromBody] ApplyDiscountDto dto)
    {
        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(rawId, out int userId))
            return Json(new DiscountResponse { Success = false, Error = "Not authenticated." });

        var item = await _itemRepository.GetByIdAsync(dto.ItemId);
        if (item is null)
            return Json(new DiscountResponse { Success = false, Error = "Item not found." });

        // Verify ownership
        if (item.UserId != userId)
            return Json(new DiscountResponse { Success = false, Error = "You don't own this item." });

        // Check if item is available (not reserved/sold)
        if (item.Status != ItemStatus.Available && item.Status != ItemStatus.Disabled)
            return Json(new DiscountResponse { Success = false, Error = "Only available items can be discounted." });

        // Check 24-hour eligibility for P2P items
        if (item.ShopId == null)
        {
            var hoursSinceCreation = (DateTime.UtcNow - item.CreatedAt).TotalHours;
            if (hoursSinceCreation < 24)
            {
                var hoursLeft = (int)Math.Ceiling(24 - hoursSinceCreation);
                return Json(new DiscountResponse
                {
                    Success = false,
                    Error = $"Discounts are available in {hoursLeft} hour{(hoursLeft != 1 ? "s" : "")} (24 hours after listing)."
                });
            }
        }

        // Validate discount percentage
        if (dto.DiscountPercentage < 1 || dto.DiscountPercentage > 99)
            return Json(new DiscountResponse { Success = false, Error = "Discount must be between 1% and 99%." });

        // Validate expiration (if provided, must be in the future)
        if (dto.ExpiresAt.HasValue && dto.ExpiresAt.Value <= DateTime.UtcNow)
            return Json(new DiscountResponse { Success = false, Error = "Expiration date must be in the future." });

        // Store original price if not already discounted
        if (!item.OriginalPrice.HasValue)
            item.OriginalPrice = item.Price;

        // Calculate new price for the item (used for display purposes)
        var discountMultiplier = (100 - dto.DiscountPercentage) / 100m;
        var newPrice = Math.Round(item.OriginalPrice.Value * discountMultiplier, 2);

        // Apply discount to Item
        item.Price = newPrice;
        item.DiscountPercentage = dto.DiscountPercentage;
        item.DiscountedAt = DateTime.UtcNow;
        item.DiscountExpiresAt = dto.ExpiresAt;

        int updatedSkuCount = 0;

        if (item.ShopId != null)
        {
            // ── SHOP ITEM: Update ALL SKUs across all variants ─────────────────
            var allSkus = await _context.ItemVariantSkus
                .Include(s => s.Variant)
                .Where(s => s.Variant != null && s.Variant.ItemId == item.Id)
                .ToListAsync();

            foreach (var sku in allSkus)
            {
                // Store original SKU price if not already stored
                if (!sku.OriginalPrice.HasValue)
                    sku.OriginalPrice = sku.Price;

                // Apply the same discount percentage to each SKU
                var skuDiscountMultiplier = (100 - dto.DiscountPercentage) / 100m;
                sku.Price = Math.Round(sku.OriginalPrice.Value * skuDiscountMultiplier, 2);
                sku.DiscountPercentage = dto.DiscountPercentage;
                updatedSkuCount++;
            }

            _logger.LogInformation(
                "User {UserId} applied {DiscountPercent}% discount to Shop Item {ItemId}. " +
                "Updated {SkuCount} SKUs. New base price: ₱{NewPrice}. Expires: {Expires}",
                userId, dto.DiscountPercentage, item.Id, updatedSkuCount, newPrice,
                dto.ExpiresAt?.ToString("O") ?? "never");
        }
        else
        {
            // ── P2P ITEM: Update the single default SKU ────────────────────────
            var defaultSku = await _context.ItemVariantSkus
                .FirstOrDefaultAsync(s => s.Variant != null && s.Variant.ItemId == item.Id);

            if (defaultSku != null)
            {
                if (!defaultSku.OriginalPrice.HasValue)
                    defaultSku.OriginalPrice = defaultSku.Price;

                defaultSku.Price = newPrice;
                defaultSku.DiscountPercentage = dto.DiscountPercentage;
                updatedSkuCount = 1;
            }

            _logger.LogInformation(
                "User {UserId} applied {DiscountPercent}% discount to P2P Item {ItemId}. " +
                "New price: ₱{NewPrice}. Expires: {Expires}",
                userId, dto.DiscountPercentage, item.Id, newPrice,
                dto.ExpiresAt?.ToString("O") ?? "never");
        }

        await _itemRepository.UpdateAsync(item);

        return Json(new DiscountResponse
        {
            Success = true,
            NewPrice = newPrice,
            OriginalPrice = item.OriginalPrice.Value,
            DiscountPercentage = dto.DiscountPercentage,
            SavingsAmount = item.OriginalPrice.Value - newPrice,
            ExpiresAt = dto.ExpiresAt?.ToString("MMM d, yyyy 'at' h:mm tt"),
            IsIndefinite = !dto.ExpiresAt.HasValue
        });
    }

    /// <summary>
    /// Removes the discount from an item, restoring the original prices.
    /// For P2P: restores Item.Price and the single default SKU.
    /// For Shop: restores Item.Price and ALL SKUs to their original prices.
    /// </summary>
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveDiscount(int itemId)
    {
        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(rawId, out int userId))
            return Json(new { success = false, error = "Not authenticated." });

        var item = await _itemRepository.GetByIdAsync(itemId);
        if (item is null)
            return Json(new { success = false, error = "Item not found." });

        // Verify ownership
        if (item.UserId != userId)
            return Json(new { success = false, error = "You don't own this item." });

        // Check if there's a discount to remove
        if (!item.OriginalPrice.HasValue)
            return Json(new { success = false, error = "This item is not discounted." });

        // Restore original price on Item
        var restoredPrice = item.OriginalPrice.Value;
        item.Price = restoredPrice;
        item.OriginalPrice = null;
        item.DiscountPercentage = null;
        item.DiscountedAt = null;
        item.DiscountExpiresAt = null;

        int restoredSkuCount = 0;

        if (item.ShopId != null)
        {
            // ── SHOP ITEM: Restore ALL SKUs ────────────────────────────────────
            var allSkus = await _context.ItemVariantSkus
                .Include(s => s.Variant)
                .Where(s => s.Variant != null && s.Variant.ItemId == item.Id)
                .ToListAsync();

            foreach (var sku in allSkus)
            {
                if (sku.OriginalPrice.HasValue)
                {
                    sku.Price = sku.OriginalPrice.Value;
                    sku.OriginalPrice = null;
                    sku.DiscountPercentage = null;
                    restoredSkuCount++;
                }
            }

            _logger.LogInformation(
                "User {UserId} removed discount from Shop Item {ItemId}. " +
                "Restored {SkuCount} SKUs to their original prices.",
                userId, item.Id, restoredSkuCount);
        }
        else
        {
            // ── P2P ITEM: Restore the single default SKU ───────────────────────
            var defaultSku = await _context.ItemVariantSkus
                .FirstOrDefaultAsync(s => s.Variant != null && s.Variant.ItemId == item.Id);

            if (defaultSku != null && defaultSku.OriginalPrice.HasValue)
            {
                defaultSku.Price = defaultSku.OriginalPrice.Value;
                defaultSku.OriginalPrice = null;
                defaultSku.DiscountPercentage = null;
                restoredSkuCount = 1;
            }

            _logger.LogInformation(
                "User {UserId} removed discount from P2P Item {ItemId}. Price restored to ₱{Price}.",
                userId, item.Id, restoredPrice);
        }

        await _itemRepository.UpdateAsync(item);

        return Json(new
        {
            success = true,
            restoredPrice = restoredPrice,
            message = "Discount removed successfully."
        });
    }

    /// <summary>
    /// Gets the current discount info for an item.
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetDiscountInfo(int itemId)
    {
        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(rawId, out int userId))
            return Json(new { success = false, error = "Not authenticated." });

        var item = await _itemRepository.GetByIdAsync(itemId);
        if (item is null)
            return Json(new { success = false, error = "Item not found." });

        // Verify ownership
        if (item.UserId != userId)
            return Json(new { success = false, error = "You don't own this item." });

        var viewModel = new DiscountedItemViewModel
        {
            ItemId = item.Id,
            CurrentPrice = item.Price,
            OriginalPrice = item.OriginalPrice ?? item.Price,
            DiscountPercentage = item.DiscountPercentage ?? 0,
            SavingsAmount = item.SavingsAmount,
            DiscountExpiresAt = item.DiscountExpiresAt,
            HasActiveDiscount = item.HasActiveDiscount
        };

        return Json(new
        {
            success = true,
            hasDiscount = item.HasActiveDiscount,
            canBeDiscounted = item.CanBeDiscounted,
            currentPrice = viewModel.FormattedCurrentPrice,
            originalPrice = viewModel.FormattedOriginalPrice,
            discountPercentage = item.DiscountPercentage,
            discountBadge = viewModel.DiscountBadgeText,
            savings = viewModel.FormattedSavings,
            expiresAt = item.DiscountExpiresAt?.ToString("MMM d, yyyy"),
            expiryText = viewModel.ExpiryText,
            isIndefinite = viewModel.IsIndefinite
        });
    }

    // ── GET ITEM (Stealable) ──────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GetItem(int id)
    {
        // Redirect riders to their dashboard
        if (IsRider())
        {
            _logger.LogWarning("Rider attempted to access Items/GetItem");
            return RedirectToAction("Index", "Rider");
        }

        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(rawId, out int buyerId)) return Unauthorized();

        if (!await HasCompleteProfileAsync(buyerId))
            return RedirectToCompleteProfile("claim items");

        var item = await _itemRepository.GetByIdAsync(id);
        if (item is null) return NotFound();

        if (item.UserId == buyerId)
        { TempData["ErrorMessage"] = "You cannot claim your own listing."; return RedirectToAction(nameof(Details), new { id }); }

        if (item.ListingType != ListingType.Stealable)
        { TempData["ErrorMessage"] = "This listing does not support the Get/Steal flow."; return RedirectToAction(nameof(Details), new { id }); }

        if (item.Status != ItemStatus.Available)
        {
            TempData["ErrorMessage"] = item.Status == ItemStatus.Reserved
                ? "This item has already been claimed. You may still Steal it before the timer expires."
                : "This item has already been sold.";
            return RedirectToAction(nameof(Details), new { id });
        }

        item.CurrentWinnerId = buyerId;
        item.OriginalGetterUserId = null;
        item.Status = ItemStatus.Reserved;
        item.StealEndsAt = DateTime.UtcNow.AddHours(item.StealDurationHours!.Value);

        await _itemRepository.UpdateAsync(item);

        _logger.LogInformation("User {BuyerId} claimed Item {ItemId}. Steal closes at {StealEndsAt} UTC.",
            buyerId, item.Id, item.StealEndsAt);

        TempData["SuccessMessage"] =
            $"You've claimed '{item.Title}'! Another buyer can Steal it within " +
            $"{item.StealDurationHours} hour(s). If no one does, you'll have 2 hours to finalise.";

        return RedirectToAction(nameof(Details), new { id });
    }

    // ── CANCEL GET ────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelGet(int id)
    {
        // Redirect riders to their dashboard
        if (IsRider())
        {
            _logger.LogWarning("Rider attempted to access Items/CancelGet");
            return RedirectToAction("Index", "Rider");
        }

        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(rawId, out int userId)) return Unauthorized();

        var item = await _itemRepository.GetByIdAsync(id);
        if (item is null) return NotFound();

        if (item.CurrentWinnerId != userId)
        {
            TempData["ErrorMessage"] = "You are not the current holder of this item.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (item.Status != ItemStatus.Reserved)
        {
            TempData["ErrorMessage"] = "This item is not in a cancellable reserved state.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (item.IsInFinalizeWindow)
        {
            TempData["ErrorMessage"] =
                "The steal window has closed and your finalize window is open. " +
                "Please complete your purchase rather than cancelling.";
            return RedirectToAction(nameof(Details), new { id });
        }

        item.Status = ItemStatus.Available;
        item.CurrentWinnerId = null;
        item.OriginalGetterUserId = null;
        item.StealEndsAt = null;

        await _itemRepository.UpdateAsync(item);

        _logger.LogInformation(
            "User {UserId} cancelled Get on Item {ItemId}. Item returned to Available.",
            userId, item.Id);

        TempData["InfoMessage"] =
            $"Your reservation on '{item.Title}' has been cancelled. The item is now available again.";

        return RedirectToAction(nameof(Details), new { id });
    }

    // ── STEAL ITEM ────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StealItem(int id)
    {
        // Redirect riders to their dashboard
        if (IsRider())
        {
            _logger.LogWarning("Rider attempted to access Items/StealItem");
            return RedirectToAction("Index", "Rider");
        }

        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(rawId, out int stealerId)) return Unauthorized();

        if (!await HasCompleteProfileAsync(stealerId))
            return RedirectToCompleteProfile("steal items");

        var item = await _itemRepository.GetByIdAsync(id);
        if (item is null) return NotFound();

        if (item.UserId == stealerId)
        { TempData["ErrorMessage"] = "You cannot steal your own listing."; return RedirectToAction(nameof(Details), new { id }); }

        if (item.CurrentWinnerId == stealerId)
        { TempData["ErrorMessage"] = "You already hold this item — head to checkout to finalise."; return RedirectToAction(nameof(Details), new { id }); }

        if (item.ListingType != ListingType.Stealable)
        { TempData["ErrorMessage"] = "This listing does not support the Steal flow."; return RedirectToAction(nameof(Details), new { id }); }

        if (item.Status != ItemStatus.Reserved)
        {
            TempData["ErrorMessage"] = item.Status == ItemStatus.Available
                ? "No one has claimed this item yet. Use 'Get' to claim it at the base price."
                : "This item has already been sold — it can no longer be stolen.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (item.StealEndsAt.HasValue && DateTime.UtcNow > item.StealEndsAt.Value)
        {
            TempData["ErrorMessage"] = "The Steal window has expired. The original buyer has the first right to finalise.";
            return RedirectToAction(nameof(Details), new { id });
        }

        const decimal StealPremium = 50m;
        int previousWinnerId = item.CurrentWinnerId!.Value;

        item.Price += StealPremium;
        item.Status = ItemStatus.StolenPendingCheckout;
        item.OriginalGetterUserId = previousWinnerId;
        item.CurrentWinnerId = stealerId;

        await _itemRepository.UpdateAsync(item);

        _logger.LogInformation(
            "User {StealerId} stole Item {ItemId} from User {PreviousWinnerId}. " +
            "New price: ₱{NewPrice}. Awaiting checkout.",
            stealerId, item.Id, previousWinnerId, item.Price);

        TempData["SuccessMessage"] =
            $"You stole '{item.Title}'! The price is now ₱{item.Price:N2}. " +
            "Please complete your purchase below.";

        return RedirectToAction("Checkout", "Orders", new { itemId = item.Id });
    }

    // ── CANCEL STEAL ──────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelSteal(int id)
    {
        // Redirect riders to their dashboard
        if (IsRider())
        {
            _logger.LogWarning("Rider attempted to access Items/CancelSteal");
            return RedirectToAction("Index", "Rider");
        }

        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(rawId, out int userId)) return Unauthorized();

        var item = await _itemRepository.GetByIdAsync(id);
        if (item is null) return NotFound();

        if (item.CurrentWinnerId != userId)
        {
            TempData["ErrorMessage"] = "You are not the current holder of this item.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (item.Status != ItemStatus.StolenPendingCheckout)
        {
            TempData["ErrorMessage"] = "This item is not in a cancellable steal state.";
            return RedirectToAction(nameof(Details), new { id });
        }

        const decimal StealPremium = 50m;

        int restoredGetterId = item.OriginalGetterUserId!.Value;

        item.Price -= StealPremium;
        item.Status = ItemStatus.Reserved;
        item.CurrentWinnerId = restoredGetterId;
        item.OriginalGetterUserId = null;

        await _itemRepository.UpdateAsync(item);

        _logger.LogInformation(
            "User {StealerId} cancelled steal on Item {ItemId}. " +
            "Reservation restored to User {OriginalGetterId}.",
            userId, item.Id, restoredGetterId);

        TempData["InfoMessage"] =
            $"Steal cancelled. '{item.Title}' has been returned to its previous holder at the original price.";

        return RedirectToAction(nameof(Details), new { id });
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<(Item? item, IActionResult? actionResult)> GetOwnedItemAsync(int id)
    {
        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(rawId, out int userId)) return (null, Unauthorized());

        var item = await _itemRepository.GetByIdAsync(id);
        if (item is null) return (null, NotFound());
        if (item.UserId != userId) return (null, Forbid());

        return (item, null);
    }

    private async Task<(string? url, string? error)> SaveImageAsync(IFormFile file)
    {
        var allowed = new[] { "image/jpeg", "image/png", "image/webp" };
        if (!allowed.Contains(file.ContentType))
            return (null, "Only JPEG, PNG, or WebP images are accepted.");

        if (file.Length > 5 * 1024 * 1024)
            return (null, "Image must be smaller than 5 MB.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = $"{Guid.NewGuid()}{ext}";
        var folder = Path.Combine(_env.WebRootPath, "uploads", "items");

        Directory.CreateDirectory(folder);

        var fullPath = Path.Combine(folder, fileName);
        await using var stream = System.IO.File.Create(fullPath);
        await file.CopyToAsync(stream);

        return ($"/uploads/items/{fileName}", null);
    }

    private void DeleteImageFile(string? imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl)) return;
        var fullPath = Path.Combine(_env.WebRootPath, "uploads", "items", Path.GetFileName(imageUrl));
        if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
    }

    private static ItemEditViewModel MapToEditViewModel(Item item) => new()
    {
        Id = item.Id,
        Title = item.Title,
        Description = item.Description,
        Price = item.Price,
        Category = item.Category,
        Condition = item.Condition,
        Size = item.Size,
        ExistingImageUrls = item.ImageUrls.ToList(),
        AllowDelivery = item.AllowDelivery,
        AllowHalfway = item.AllowHalfway,
        AllowPickup = item.AllowPickup
    };
}