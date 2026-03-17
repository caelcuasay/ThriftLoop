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

        _logger.LogInformation(
            "Item {ItemId} details viewed by user {UserId}.",
            item.Id,
            currentUserId?.ToString() ?? "anonymous");

        return View(item);
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
            UserId = userId
        };

        await _itemRepository.AddAsync(item);
        _logger.LogInformation("User {UserId} created Item {ItemId}.", userId, item.Id);

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
        // item.UserId and item.CreatedAt are intentionally not touched.

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