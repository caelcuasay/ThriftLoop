// Areas/Mobile/Controllers/ItemsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ThriftLoop.Data;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;
using ThriftLoop.ViewModels;
using ThriftLoop.Enums;
using ThriftLoop.Services.Interface;

namespace ThriftLoop.Areas.Mobile.Controllers;

[Area("Mobile")]
[Route("mobile/[controller]/[action]")]
[Authorize]
public class ItemsController : Controller
{
    private readonly IItemRepository _itemRepository;
    private readonly ILogger<ItemsController> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly ApplicationDbContext _context;
    private readonly IChatService _chatService;

    public ItemsController(
        IItemRepository itemRepository,
        IWebHostEnvironment env,
        ILogger<ItemsController> logger,
        ApplicationDbContext context,
        IChatService chatService)
    {
        _itemRepository = itemRepository;
        _env = env;
        _logger = logger;
        _context = context;
        _chatService = chatService;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private int GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out int id) ? id : 0;
    }

    private bool IsRider()
    {
        return User.HasClaim(c => c.Type == "IsRider" && c.Value == "true");
    }

    private async Task<bool> HasCompleteProfileAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        return user is not null
            && !string.IsNullOrWhiteSpace(user.PhoneNumber)
            && !string.IsNullOrWhiteSpace(user.Address);
    }

    private IActionResult RedirectToCompleteProfile(string action)
    {
        TempData["ProfileIncomplete"] =
            $"Please add your phone number and address before you can {action}.";
        return RedirectToAction("Index", "User", new { area = "Mobile" });
    }

    private async Task<(Item? item, IActionResult? actionResult)> GetOwnedItemAsync(int id)
    {
        var userId = GetUserId();
        if (userId == 0) return (null, Unauthorized());

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

    // ── INDEX (My Listings) ──────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (IsRider())
        {
            _logger.LogWarning("Mobile: Rider attempted to access Items/Index");
            return RedirectToAction("Index", "Home", new { area = "Mobile" });
        }

        var userId = GetUserId();
        if (userId == 0) return Unauthorized();

        var items = await _itemRepository.GetItemsByUserIdAsync(userId);
        _logger.LogInformation("Mobile: User {UserId} viewed My Listings ({Count} items).", userId, items.Count);
        return View(items);
    }

    // ── DETAILS ──────────────────────────────────────────────────────────────

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

        _logger.LogInformation("Mobile: Item {ItemId} details viewed by user {UserId}.",
            item.Id, currentUserId?.ToString() ?? "anonymous");

        return View(item);
    }

    // ── BUY NOW ──────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> BuyNow(int id, FulfillmentMethod? fulfillmentMethod = null)
    {
        if (IsRider())
        {
            _logger.LogWarning("Mobile: Rider attempted to access Items/BuyNow");
            return RedirectToAction("Index", "Home", new { area = "Mobile" });
        }

        var userId = GetUserId();
        if (userId == 0) return Unauthorized();

        if (!await HasCompleteProfileAsync(userId))
            return RedirectToCompleteProfile("purchase items");

        return RedirectToAction("Checkout", "Orders", new { area = "Mobile", itemId = id, fulfillmentMethod });
    }

    // ── CONTACT SELLER ───────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> ContactSeller(int id, string? message = null)
    {
        if (IsRider())
        {
            _logger.LogWarning("Mobile: Rider attempted to access Items/ContactSeller");
            return RedirectToAction("Index", "Home", new { area = "Mobile" });
        }

        var buyerId = GetUserId();
        if (buyerId == 0) return Unauthorized();

        if (!await HasCompleteProfileAsync(buyerId))
            return RedirectToCompleteProfile("contact sellers");

        var item = await _itemRepository.GetByIdWithUserAsync(id);
        if (item is null) return NotFound();

        if (item.UserId == buyerId)
        {
            TempData["ErrorMessage"] = "You cannot contact yourself about your own item.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (item.Status != ItemStatus.Available)
        {
            TempData["ErrorMessage"] = "This item is no longer available.";
            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            var conversation = await _chatService.ContactSellerAboutItemAsync(buyerId, id, message);

            _logger.LogInformation(
                "Mobile: User {BuyerId} contacted seller {SellerId} about Item {ItemId}. Conversation: {ConversationId}",
                buyerId, item.UserId, id, conversation.Id);

            TempData["SuccessMessage"] = $"Chat opened with {item.User?.Email?.Split('@')[0] ?? "seller"} about '{item.Title}'.";
            return RedirectToAction("Conversation", "Chat", new { area = "Mobile", id = conversation.Id });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Mobile: Failed to contact seller for Item {ItemId}", id);
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    // ── CREATE ───────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (IsRider())
        {
            _logger.LogWarning("Mobile: Rider attempted to access Items/Create");
            return RedirectToAction("Index", "Home", new { area = "Mobile" });
        }

        var userId = GetUserId();
        if (userId == 0) return Unauthorized();

        if (!await HasCompleteProfileAsync(userId))
            return RedirectToCompleteProfile("list items for sale");

        return View(new ItemCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ItemCreateViewModel viewModel)
    {
        if (IsRider())
        {
            _logger.LogWarning("Mobile: Rider attempted to access Items/Create (POST)");
            return RedirectToAction("Index", "Home", new { area = "Mobile" });
        }

        var userId = GetUserId();
        if (userId == 0) return Unauthorized();

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
            AllowDelivery = viewModel.AllowDelivery,
            AllowHalfway = viewModel.AllowHalfway,
            AllowPickup = viewModel.AllowPickup
        };

        await _itemRepository.AddAsync(item);
        _logger.LogInformation("Mobile: User {UserId} created Item {ItemId} ({ListingType}).", userId, item.Id, item.ListingType);

        TempData["SuccessMessage"] = $"'{item.Title}' was listed successfully!";
        return RedirectToAction(nameof(Index));
    }

    // ── EDIT ─────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        if (IsRider())
        {
            _logger.LogWarning("Mobile: Rider attempted to access Items/Edit");
            return RedirectToAction("Index", "Home", new { area = "Mobile" });
        }

        var (item, actionResult) = await GetOwnedItemAsync(id);
        if (actionResult is not null) return actionResult;
        return View(MapToEditViewModel(item!));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ItemEditViewModel viewModel)
    {
        if (IsRider())
        {
            _logger.LogWarning("Mobile: Rider attempted to access Items/Edit (POST)");
            return RedirectToAction("Index", "Home", new { area = "Mobile" });
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
        item.AllowDelivery = viewModel.AllowDelivery;
        item.AllowHalfway = viewModel.AllowHalfway;
        item.AllowPickup = viewModel.AllowPickup;

        await _itemRepository.UpdateAsync(item);
        _logger.LogInformation("Mobile: User {UserId} updated Item {ItemId}.", item.UserId, item.Id);

        TempData["SuccessMessage"] = $"'{item.Title}' was updated successfully!";
        return RedirectToAction(nameof(Index));
    }

    // ── DELETE ───────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        if (IsRider())
        {
            _logger.LogWarning("Mobile: Rider attempted to access Items/Delete");
            return RedirectToAction("Index", "Home", new { area = "Mobile" });
        }

        var (item, actionResult) = await GetOwnedItemAsync(id);
        if (actionResult is not null) return actionResult;
        return View(item);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        if (IsRider())
        {
            _logger.LogWarning("Mobile: Rider attempted to access Items/DeleteConfirmed");
            return RedirectToAction("Index", "Home", new { area = "Mobile" });
        }

        var (item, actionResult) = await GetOwnedItemAsync(id);
        if (actionResult is not null) return actionResult;

        bool hasOrders = await _context.Orders.AnyAsync(o => o.ItemId == id);
        if (hasOrders)
        {
            TempData["ErrorMessage"] =
                $"'{item!.Title}' cannot be deleted because it has associated orders. Contact support if you need this listing removed.";
            return RedirectToAction(nameof(Index));
        }

        var itemLikes = await _context.ItemLikes.Where(il => il.ItemId == id).ToListAsync();
        if (itemLikes.Any())
        {
            _context.ItemLikes.RemoveRange(itemLikes);
            await _context.SaveChangesAsync();
        }

        var cartItems = await _context.CartItems.Where(ci => ci.ItemId == id).ToListAsync();
        if (cartItems.Any())
        {
            _context.CartItems.RemoveRange(cartItems);
            await _context.SaveChangesAsync();
        }

        foreach (var url in item!.ImageUrls)
            DeleteImageFile(url);

        await _itemRepository.DeleteAsync(id);

        _logger.LogInformation("Mobile: User {UserId} deleted Item {ItemId}.", item.UserId, item.Id);

        TempData["SuccessMessage"] = $"'{item.Title}' was deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ── GET ITEM (Stealable) ─────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GetItem(int id)
    {
        if (IsRider())
        {
            _logger.LogWarning("Mobile: Rider attempted to access Items/GetItem");
            return RedirectToAction("Index", "Home", new { area = "Mobile" });
        }

        var buyerId = GetUserId();
        if (buyerId == 0) return Unauthorized();

        if (!await HasCompleteProfileAsync(buyerId))
            return RedirectToCompleteProfile("claim items");

        var item = await _itemRepository.GetByIdAsync(id);
        if (item is null) return NotFound();

        if (item.UserId == buyerId)
        {
            TempData["ErrorMessage"] = "You cannot claim your own listing.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (item.ListingType != ListingType.Stealable)
        {
            TempData["ErrorMessage"] = "This listing does not support the Get/Steal flow.";
            return RedirectToAction(nameof(Details), new { id });
        }

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

        TempData["SuccessMessage"] =
            $"You've claimed '{item.Title}'! Another buyer can Steal it within {item.StealDurationHours} hour(s).";

        return RedirectToAction(nameof(Details), new { id });
    }

    // ── CANCEL GET ───────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelGet(int id)
    {
        if (IsRider())
        {
            _logger.LogWarning("Mobile: Rider attempted to access Items/CancelGet");
            return RedirectToAction("Index", "Home", new { area = "Mobile" });
        }

        var userId = GetUserId();
        if (userId == 0) return Unauthorized();

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

        item.Status = ItemStatus.Available;
        item.CurrentWinnerId = null;
        item.OriginalGetterUserId = null;
        item.StealEndsAt = null;

        await _itemRepository.UpdateAsync(item);

        TempData["InfoMessage"] = $"Your reservation on '{item.Title}' has been cancelled. The item is now available again.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ── STEAL ITEM ───────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StealItem(int id)
    {
        if (IsRider())
        {
            _logger.LogWarning("Mobile: Rider attempted to access Items/StealItem");
            return RedirectToAction("Index", "Home", new { area = "Mobile" });
        }

        var stealerId = GetUserId();
        if (stealerId == 0) return Unauthorized();

        if (!await HasCompleteProfileAsync(stealerId))
            return RedirectToCompleteProfile("steal items");

        var item = await _itemRepository.GetByIdAsync(id);
        if (item is null) return NotFound();

        if (item.UserId == stealerId)
        {
            TempData["ErrorMessage"] = "You cannot steal your own listing.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (item.CurrentWinnerId == stealerId)
        {
            TempData["ErrorMessage"] = "You already hold this item — head to checkout to finalise.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (item.ListingType != ListingType.Stealable)
        {
            TempData["ErrorMessage"] = "This listing does not support the Steal flow.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (item.Status != ItemStatus.Reserved)
        {
            TempData["ErrorMessage"] = item.Status == ItemStatus.Available
                ? "No one has claimed this item yet. Use 'Get' to claim it at the base price."
                : "This item has already been sold.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (item.StealEndsAt.HasValue && DateTime.UtcNow > item.StealEndsAt.Value)
        {
            TempData["ErrorMessage"] = "The Steal window has expired.";
            return RedirectToAction(nameof(Details), new { id });
        }

        const decimal StealPremium = 50m;
        int previousWinnerId = item.CurrentWinnerId!.Value;

        item.Price += StealPremium;
        item.Status = ItemStatus.StolenPendingCheckout;
        item.OriginalGetterUserId = previousWinnerId;
        item.CurrentWinnerId = stealerId;

        await _itemRepository.UpdateAsync(item);

        TempData["SuccessMessage"] = $"You stole '{item.Title}'! Price: ₱{item.Price:N2}. Complete your purchase below.";
        return RedirectToAction("Checkout", "Orders", new { area = "Mobile", itemId = item.Id });
    }

    // ── CANCEL STEAL ─────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelSteal(int id)
    {
        if (IsRider())
        {
            _logger.LogWarning("Mobile: Rider attempted to access Items/CancelSteal");
            return RedirectToAction("Index", "Home", new { area = "Mobile" });
        }

        var userId = GetUserId();
        if (userId == 0) return Unauthorized();

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

        TempData["InfoMessage"] = $"Steal cancelled. '{item.Title}' returned to previous holder at original price.";
        return RedirectToAction(nameof(Details), new { id });
    }
}