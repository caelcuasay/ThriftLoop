using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ThriftLoop.Repositories.Interface;
using ThriftLoop.ViewModels;

namespace ThriftLoop.Controllers;

public class HomeController : Controller
{
    private readonly IItemRepository _itemRepository;
    private readonly ILogger<HomeController> _logger;

    public HomeController(IItemRepository itemRepository, ILogger<HomeController> logger)
    {
        _itemRepository = itemRepository;
        _logger = logger;
    }

    // ── For You feed ───────────────────────────────────────────────────────

    /// <summary>
    /// GET / — public feed showing all listed items.
    /// Authenticated users see ownership state per card (no buy buttons on own items).
    /// Anonymous visitors see buy buttons on every card.
    /// </summary>
    public async Task<IActionResult> Index()
    {
        // Resolve the current user's ID — null for anonymous visitors.
        int? currentUserId = null;
        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(rawId, out int parsedId))
            currentUserId = parsedId;

        var items = await _itemRepository.GetAllAsync();

        var viewModel = new HomeIndexViewModel
        {
            Items = items,
            CurrentUserId = currentUserId
        };

        return View(viewModel);
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ThriftLoop.Models.ErrorViewModel
    {
        RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier
    });
}