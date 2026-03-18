using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;
using ThriftLoop.ViewModels;

namespace ThriftLoop.Controllers;

[Authorize]   // Every action in this controller requires an authenticated user.
public class ItemsController : Controller
{
    private readonly IItemRepository _itemRepository;
    private readonly ILogger<ItemsController> _logger;
    private readonly IWebHostEnvironment _env;

    public ItemsController(
        IItemRepository itemRepository,
        IWebHostEnvironment env,
        ILogger<ItemsController> logger)
    {
        _itemRepository = itemRepository;
        _env = env;
        _logger = logger;
    }

    // ── INDEX (My Listings) ────────────────────────────────────────────────

    /// <summary>GET /Items — shows all listings posted by the current user.</summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(rawId, out int userId))
            return Unauthorized();

        var items = await _itemRepository.GetItemsByUserIdAsync(userId);

        _logger.LogInformation(
            "User {UserId} viewed My Listings ({Count} items).", userId, items.Count);

        return View(items);
    }

    // ── DETAILS ────────────────────────────────────────────────────────────

    /// <summary>
    /// GET /Items/Details/{id} — public detail page for a single listing.
    /// Loads the seller (User) via eager loading so the view can display
    /// who posted the item. Ownership is surfaced via ViewBag.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Details(int id)
    {
        var item = await _itemRepository.GetByIdWithUserAsync(id);
        if (item is null)
            return NotFound();

        // Resolve current user — null when the visitor is anonymous.
        int? currentUserId = null;
        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(rawId, out int parsedId))
            currentUserId = parsedId;

        ViewBag.IsOwner = currentUserId.HasValue && item.UserId == currentUserId.Value;
        ViewBag.IsAnonymous = !currentUserId.HasValue;
        ViewBag.CurrentUserId = currentUserId;

        // Stealable-specific state for the view.
        ViewBag.IsCurrentWinner = currentUserId.HasValue && item.CurrentWinnerId == currentUserId.Value;

        _logger.LogInformation(
            "Item {ItemId} details viewed by user {UserId}.",
            item.Id,
            currentUserId?.ToString() ?? "anonymous");

        return View(item);
    }

    // ── BUY NOW (Standard — redirect to Checkout) ─────────────────────────

    /// <summary>
    /// GET /Items/BuyNow/{id}
    /// Entry point for authenticated non-owner buyers on Standard listings.
    /// Performs a thin redirect to the OrdersController Checkout action,
    /// which enforces all access guards (owner check, status, idempotency).
    ///
    /// Using a dedicated action here keeps the Details view's routing
    /// symmetric with the rest of the Items controller and makes the
    /// Standard purchase flow easy to extend later (e.g. add analytics,
    /// pre-purchase checks, or a cart step) without touching the view.
    /// </summary>
    [HttpGet]
    public IActionResult BuyNow(int id)
    {
        return RedirectToAction("Checkout", "Orders", new { itemId = id });
    }

    // ── CREATE ─────────────────────────────────────────────────────────────

    /// <summary>GET /Items/Create — renders the empty Create form.</summary>
    [HttpGet]
    public IActionResult Create()
    {
        return View(new ItemCreateViewModel());
    }

    /// <summary>POST /Items/Create — validates, maps ViewModel → Model, saves.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ItemCreateViewModel viewModel)
    {
        if (!ModelState.IsValid)
            return View(viewModel);

        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(rawId, out int userId))
            return Unauthorized();

        string? imageUrl = null;

        if (viewModel.Image is not null && viewModel.Image.Length > 0)
        {
            var (saved, error) = await SaveImageAsync(viewModel.Image);
            if (error is not null)
            {
                ModelState.AddModelError(nameof(viewModel.Image), error);
                return View(viewModel);
            }
            imageUrl = saved;
        }

        var item = new Item
        {
            Title = viewModel.Title,
            Description = viewModel.Description,
            Price = viewModel.Price,
            Category = viewModel.Category,
            Condition = viewModel.Condition,
            Size = string.IsNullOrWhiteSpace(viewModel.Size) ? null : viewModel.Size,
            ImageUrl = imageUrl,
            CreatedAt = DateTime.UtcNow,
            UserId = userId,

            // ── Stealable fields ──────────────────────────────────────────
            // ListingType is stored so the Details view and purchase actions
            // know how to render and guard each interaction.
            ListingType = viewModel.IsStealable ? ListingType.Stealable : ListingType.Standard,
            StealDurationHours = viewModel.IsStealable ? viewModel.StealDurationHours : null,

            // StealEndsAt is intentionally null at creation time.
            // It is calculated and persisted when the first buyer clicks "Get".
            StealEndsAt = null,
            CurrentWinnerId = null,
            Status = ItemStatus.Available
        };

        await _itemRepository.AddAsync(item);
        _logger.LogInformation("User {UserId} created Item {ItemId} (type: {ListingType}).",
            userId, item.Id, item.ListingType);

        TempData["SuccessMessage"] = $"'{item.Title}' was listed successfully!";
        return RedirectToAction(nameof(Index));
    }

    // ── EDIT ───────────────────────────────────────────────────────────────

    /// <summary>GET /Items/Edit/{id} — renders the pre-populated Edit form.</summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var (item, actionResult) = await GetOwnedItemAsync(id);
        if (actionResult is not null) return actionResult;

        var viewModel = MapToEditViewModel(item!);
        return View(viewModel);
    }

    /// <summary>POST /Items/Edit/{id} — validates and saves changes.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ItemEditViewModel viewModel)
    {
        if (id != viewModel.Id)
            return BadRequest();

        if (!ModelState.IsValid)
            return View(viewModel);

        var (item, actionResult) = await GetOwnedItemAsync(id);
        if (actionResult is not null) return actionResult;

        string? imageUrl = item!.ImageUrl;

        if (viewModel.RemoveImage)
        {
            DeleteImageFile(imageUrl);
            imageUrl = null;
        }
        else if (viewModel.Image is not null && viewModel.Image.Length > 0)
        {
            var (saved, error) = await SaveImageAsync(viewModel.Image);
            if (error is not null)
            {
                ModelState.AddModelError(nameof(viewModel.Image), error);
                return View(viewModel);
            }
            DeleteImageFile(imageUrl);
            imageUrl = saved;
        }

        item.Title = viewModel.Title;
        item.Description = viewModel.Description;
        item.Price = viewModel.Price;
        item.Category = viewModel.Category;
        item.Condition = viewModel.Condition;
        item.Size = string.IsNullOrWhiteSpace(viewModel.Size) ? null : viewModel.Size;
        item.ImageUrl = imageUrl;
        // item.UserId, item.CreatedAt, and all Stealable fields are intentionally not touched.

        await _itemRepository.UpdateAsync(item);
        _logger.LogInformation("User {UserId} updated Item {ItemId}.", item.UserId, item.Id);

        TempData["SuccessMessage"] = $"'{item.Title}' was updated successfully!";
        return RedirectToAction(nameof(Index));
    }

    // ── DELETE ─────────────────────────────────────────────────────────────

    /// <summary>GET /Items/Delete/{id} — renders the Delete confirmation page.</summary>
    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var (item, actionResult) = await GetOwnedItemAsync(id);
        if (actionResult is not null) return actionResult;

        return View(item);
    }

    /// <summary>POST /Items/Delete/{id} — performs the deletion after confirmation.</summary>
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var (item, actionResult) = await GetOwnedItemAsync(id);
        if (actionResult is not null) return actionResult;

        DeleteImageFile(item!.ImageUrl);
        await _itemRepository.DeleteAsync(id);

        _logger.LogInformation("User {UserId} deleted Item {ItemId}.", item.UserId, item.Id);

        TempData["SuccessMessage"] = $"'{item.Title}' was deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ── GET ITEM (Stealable — claim the base price) ────────────────────────

    /// <summary>
    /// POST /Items/GetItem/{id}
    /// Marks the current authenticated buyer as the <see cref="Item.CurrentWinnerId"/>
    /// at the base price, sets the item to <see cref="ItemStatus.Reserved"/>, and
    /// starts the Steal countdown by writing <see cref="Item.StealEndsAt"/>.
    ///
    /// Guard conditions (each returns an error TempData and redirects to Details):
    ///   • Item does not exist → 404.
    ///   • The buyer is the seller → cannot buy your own listing.
    ///   • The item is not a Stealable listing → wrong flow.
    ///   • The item is not Available → already reserved or sold.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GetItem(int id)
    {
        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(rawId, out int buyerId))
            return Unauthorized();

        var item = await _itemRepository.GetByIdAsync(id);
        if (item is null)
            return NotFound();

        // ── Guard: seller cannot buy their own listing ────────────────────
        if (item.UserId == buyerId)
        {
            TempData["ErrorMessage"] = "You cannot claim your own listing.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── Guard: only Stealable listings use this flow ──────────────────
        if (item.ListingType != ListingType.Stealable)
        {
            TempData["ErrorMessage"] = "This listing does not support the Get/Steal flow.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── Guard: item must still be Available ───────────────────────────
        if (item.Status != ItemStatus.Available)
        {
            TempData["ErrorMessage"] = item.Status == ItemStatus.Reserved
                ? "This item has already been claimed. You may still Steal it before the timer expires."
                : "This item has already been sold.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── Claim the item ────────────────────────────────────────────────
        item.CurrentWinnerId = buyerId;
        item.Status = ItemStatus.Reserved;
        item.StealEndsAt = DateTime.UtcNow.AddHours(item.StealDurationHours!.Value);

        await _itemRepository.UpdateAsync(item);

        _logger.LogInformation(
            "User {BuyerId} claimed (Get) Item {ItemId}. Steal window closes at {StealEndsAt} UTC.",
            buyerId, item.Id, item.StealEndsAt);

        TempData["SuccessMessage"] =
            $"You've claimed '{item.Title}'! Another buyer can Steal it within " +
            $"{item.StealDurationHours} hour(s). If no one does, you'll have 2 hours to finalise.";

        return RedirectToAction(nameof(Details), new { id });
    }

    // ── STEAL ITEM (Stealable — override the current winner) ──────────────

    /// <summary>
    /// POST /Items/StealItem/{id}
    /// Allows a second buyer to "Steal" the item from the current winner.
    /// The steal adds ₱50 to the current price, marks the item as
    /// <see cref="ItemStatus.Sold"/>, and immediately redirects the stealer
    /// to the Checkout page so they can finalise the higher-price purchase.
    ///
    /// Guard conditions:
    ///   • Item not found → 404.
    ///   • Stealer is the seller → cannot buy your own listing.
    ///   • Stealer is the current winner → you already hold this item.
    ///   • Item is not Stealable → wrong flow.
    ///   • Item is not Reserved (Available or already Sold) → nothing to steal.
    ///   • Steal window has expired → too late to steal.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StealItem(int id)
    {
        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(rawId, out int stealerId))
            return Unauthorized();

        var item = await _itemRepository.GetByIdAsync(id);
        if (item is null)
            return NotFound();

        // ── Guard: seller cannot steal their own listing ──────────────────
        if (item.UserId == stealerId)
        {
            TempData["ErrorMessage"] = "You cannot steal your own listing.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── Guard: the current winner cannot steal from themselves ────────
        if (item.CurrentWinnerId == stealerId)
        {
            TempData["ErrorMessage"] = "You already hold this item — head to checkout to finalise.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── Guard: only Stealable listings use this flow ──────────────────
        if (item.ListingType != ListingType.Stealable)
        {
            TempData["ErrorMessage"] = "This listing does not support the Steal flow.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── Guard: there must be a current winner to steal from ───────────
        if (item.Status != ItemStatus.Reserved)
        {
            TempData["ErrorMessage"] = item.Status == ItemStatus.Available
                ? "No one has claimed this item yet. Use 'Get' to claim it at the base price."
                : "This item has already been sold — it can no longer be stolen.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── Guard: steal window must still be open ────────────────────────
        if (item.StealEndsAt.HasValue && DateTime.UtcNow > item.StealEndsAt.Value)
        {
            TempData["ErrorMessage"] =
                "The Steal window has expired. The original buyer has the first right to finalise.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── Apply the steal ───────────────────────────────────────────────
        // Steal can only happen once — the item moves straight to Sold after
        // the price bump, and the stealer is redirected to Checkout immediately.
        const decimal StealPremium = 50m;

        item.Price += StealPremium;   // ₱50 is added automatically.
        item.Status = ItemStatus.Sold;

        // Record who stole (CurrentWinnerId is overwritten; the previous winner
        // is effectively outbid and should be notified via a future notification
        // service — outside the scope of this controller).
        int previousWinnerId = item.CurrentWinnerId!.Value;
        item.CurrentWinnerId = stealerId;

        await _itemRepository.UpdateAsync(item);

        _logger.LogInformation(
            "User {StealerId} stole Item {ItemId} from User {PreviousWinnerId}. " +
            "New price: ₱{NewPrice}.",
            stealerId, item.Id, previousWinnerId, item.Price);

        TempData["SuccessMessage"] =
            $"You stole '{item.Title}'! The price has been updated to ₱{item.Price:N2}. " +
            $"Please complete your purchase below.";

        return RedirectToAction("Checkout", "Orders", new { itemId = item.Id });
    }

    // ── Private helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Fetches an item by id and verifies ownership against the current user.
    /// Returns (item, null) on success or (null, actionResult) to short-circuit.
    /// </summary>
    private async Task<(Item? item, IActionResult? actionResult)> GetOwnedItemAsync(int id)
    {
        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(rawId, out int userId))
            return (null, Unauthorized());

        var item = await _itemRepository.GetByIdAsync(id);
        if (item is null)
            return (null, NotFound());

        if (item.UserId != userId)
            return (null, Forbid());

        return (item, null);
    }

    /// <summary>
    /// Validates and saves an uploaded image file to wwwroot/uploads/items/.
    /// Returns the relative URL on success, or an error message string on failure.
    /// </summary>
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

    /// <summary>
    /// Deletes an image file from disk given its relative URL.
    /// Silently no-ops if the URL is null/empty or the file does not exist.
    /// </summary>
    private void DeleteImageFile(string? imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl)) return;

        var fileName = Path.GetFileName(imageUrl);
        var fullPath = Path.Combine(_env.WebRootPath, "uploads", "items", fileName);

        if (System.IO.File.Exists(fullPath))
            System.IO.File.Delete(fullPath);
    }

    /// <summary>Maps a domain Item to an ItemEditViewModel.</summary>
    private static ItemEditViewModel MapToEditViewModel(Item item) => new()
    {
        Id = item.Id,
        Title = item.Title,
        Description = item.Description,
        Price = item.Price,
        Category = item.Category,
        Condition = item.Condition,
        Size = item.Size,
        ExistingImageUrl = item.ImageUrl
    };
}