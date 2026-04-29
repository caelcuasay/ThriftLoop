// Areas/Mobile/Controllers/ChatController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ThriftLoop.DTOs.Chat;
using ThriftLoop.Services.Interface;

namespace ThriftLoop.Areas.Mobile.Controllers;

[Area("Mobile")]
[Route("mobile/[controller]/[action]")]
[Authorize]
public class ChatController : Controller
{
    private readonly IChatService _chatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IChatService chatService,
        ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    /// <summary>
    /// Main mobile inbox — shows all conversations.
    /// </summary>
    [HttpGet]
    [Route("")]
    [Route("Index")]
    public async Task<IActionResult> Index(int page = 1)
    {
        var userId = CurrentUserId;
        var conversations = await _chatService.GetUserInboxAsync(userId, page);
        var unreadCount = await _chatService.GetTotalUnreadCountAsync(userId);

        ViewBag.UnreadCount = unreadCount;
        ViewBag.CurrentPage = page;

        return View(conversations);
    }

    /// <summary>
    /// View a specific conversation on mobile.
    /// </summary>
    [HttpGet]
    [Route("Conversation/{id}")]
    public async Task<IActionResult> Conversation(int id, int page = 1)
    {
        var userId = CurrentUserId;

        if (!await _chatService.CanAccessConversationAsync(id, userId))
        {
            _logger.LogWarning("Mobile: User {UserId} attempted unauthorized conversation {ConversationId}", userId, id);
            return RedirectToAction("Index");
        }

        var conversation = await _chatService.GetConversationDetailAsync(id, userId, page);
        if (conversation == null)
            return NotFound();

        ViewBag.CurrentPage = page;
        return View(conversation);
    }

    /// <summary>
    /// Start a new conversation.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start([FromForm] StartConversationDTO dto)
    {
        var userId = CurrentUserId;

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid request.";
            return RedirectToAction("Index");
        }

        try
        {
            var conversation = await _chatService.StartConversationAsync(userId, dto);
            return RedirectToAction("Conversation", new { id = conversation.Id });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction("Index");
        }
    }

    /// <summary>
    /// API: Send a message (JSON fallback).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageDTO dto)
    {
        var userId = CurrentUserId;
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var message = await _chatService.SendMessageAsync(userId, dto);
            return Ok(message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// API: Get unread count.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> UnreadCount()
    {
        var count = await _chatService.GetTotalUnreadCountAsync(CurrentUserId);
        return Json(new { unreadCount = count });
    }

    /// <summary>
    /// API: Search users to chat with.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> SearchUsers(string query)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return Json(new List<object>());

        var results = await _chatService.SearchUsersAsync(userId, query, 10);
        return Json(results);
    }

    /// <summary>
    /// Redirect to chat with a specific user.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> With(int userId, string? message = null)
    {
        var currentUserId = CurrentUserId;
        if (currentUserId == userId)
        {
            TempData["Error"] = "You cannot chat with yourself.";
            return RedirectToAction("Index");
        }

        try
        {
            var dto = new StartConversationDTO
            {
                RecipientId = userId,
                InitialMessage = message
            };
            var conversation = await _chatService.StartConversationAsync(currentUserId, dto);
            return RedirectToAction("Conversation", new { id = conversation.Id });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction("Index");
        }
    }
}