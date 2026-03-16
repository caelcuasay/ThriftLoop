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

        // ── Handle optional image upload ───────────────────────────────────
        string? imageUrl = null;

        if (viewModel.Image is not null && viewModel.Image.Length > 0)
        {
            // Validate: images only, 5 MB max
            var allowed = new[] { "image/jpeg", "image/png", "image/webp" };
            if (!allowed.Contains(viewModel.Image.ContentType))
            {
                ModelState.AddModelError(nameof(viewModel.Image),
                    "Only JPEG, PNG, or WebP images are accepted.");
                return View(viewModel);
            }

            if (viewModel.Image.Length > 5 * 1024 * 1024)
            {
                ModelState.AddModelError(nameof(viewModel.Image),
                    "Image must be smaller than 5 MB.");
                return View(viewModel);
            }

            // Build a collision-proof filename: uploads/items/{guid}.ext
            var ext = Path.GetExtension(viewModel.Image.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid()}{ext}";
            var folder = Path.Combine(_env.WebRootPath, "uploads", "items");

            Directory.CreateDirectory(folder); // safe no-op if already exists

            var fullPath = Path.Combine(folder, fileName);
            await using var stream = System.IO.File.Create(fullPath);
            await viewModel.Image.CopyToAsync(stream);

            imageUrl = $"/uploads/items/{fileName}";
        }

        // ── Map ViewModel → Domain Model ──────────────────────────────────
        var item = new Item
        {
            Title = viewModel.Title,
            Description = viewModel.Description,
            Price = viewModel.Price,
            Category = viewModel.Category,
            Condition = viewModel.Condition,
            ImageUrl = imageUrl,           // null is fine — nullable column
            CreatedAt = DateTime.UtcNow,
            UserId = userId
        };

        await _itemRepository.AddAsync(item);
        _logger.LogInformation("User {UserId} created Item {ItemId}.", userId, item.Id);

        TempData["SuccessMessage"] = $"'{item.Title}' was listed successfully!";
        return RedirectToAction(nameof(Create));
    }
}