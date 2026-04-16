// Controllers/ChatController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ThriftLoop.DTOs.Chat;
using ThriftLoop.Services.Interface;

namespace ThriftLoop.Controllers;

[Authorize]
[Route("[controller]")]
public class ChatController : Controller
{
    private readonly IChatService _chatService;
    private readonly IChatNotificationService _notificationService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IChatService chatService,
        IChatNotificationService notificationService,
        ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _notificationService = notificationService;
        _logger = logger;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    /// <summary>
    /// Main inbox view - shows all conversations.
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
    /// View a specific conversation.
    /// </summary>
    [HttpGet]
    [Route("Conversation/{id}")]
    public async Task<IActionResult> Conversation(int id, int page = 1)
    {
        var userId = CurrentUserId;

        if (!await _chatService.CanAccessConversationAsync(id, userId))
        {
            _logger.LogWarning("User {UserId} attempted to access unauthorized conversation {ConversationId}", userId, id);
            return RedirectToAction("Index");
        }

        var conversation = await _chatService.GetConversationDetailAsync(id, userId, page);
        if (conversation == null)
        {
            return NotFound();
        }

        ViewBag.CurrentPage = page;
        ViewBag.UnreadCount = await _chatService.GetTotalUnreadCountAsync(userId);

        return View(conversation);
    }

    /// <summary>
    /// Start a new conversation with a user.
    /// </summary>
    [HttpPost]
    [Route("Start")]
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
    /// API: Send a message (JSON endpoint for AJAX fallback).
    /// Primary method is via SignalR, this is a backup.
    /// </summary>
    [HttpPost]
    [Route("SendMessage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageDTO dto)
    {
        var userId = CurrentUserId;

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

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
    /// API: Get unread message count for the current user.
    /// </summary>
    [HttpGet]
    [Route("UnreadCount")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = CurrentUserId;
        var count = await _chatService.GetTotalUnreadCountAsync(userId);
        return Json(new { unreadCount = count });
    }

    /// <summary>
    /// API: Mark a conversation as read.
    /// </summary>
    [HttpPost]
    [Route("MarkAsRead/{conversationId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(int conversationId)
    {
        var userId = CurrentUserId;

        if (!await _chatService.CanAccessConversationAsync(conversationId, userId))
        {
            return Forbid();
        }

        await _chatService.MarkConversationAsReadAsync(conversationId, userId);
        return Ok();
    }

    /// <summary>
    /// API: Search for users to start a conversation with.
    /// </summary>
    [HttpGet]
    [Route("SearchUsers")]
    public async Task<IActionResult> SearchUsers(string query)
    {
        var userId = CurrentUserId;

        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
        {
            return Json(new List<object>());
        }

        var results = await _chatService.SearchUsersAsync(userId, query, 10);
        return Json(results);
    }

    /// <summary>
    /// Modal: Search for users (returns partial view for modal).
    /// </summary>
    [HttpGet]
    [Route("SearchModal")]
    public IActionResult SearchModal()
    {
        return PartialView("_UserSearchModal");
    }

    /// <summary>
    /// API: Get conversation list for sidebar refresh.
    /// </summary>
    [HttpGet]
    [Route("ConversationList")]
    public async Task<IActionResult> ConversationList(int page = 1)
    {
        var userId = CurrentUserId;
        var conversations = await _chatService.GetUserInboxAsync(userId, page);
        return PartialView("_ConversationList", conversations);
    }

    /// <summary>
    /// API: Check if a user is online.
    /// </summary>
    [HttpGet]
    [Route("IsUserOnline/{userId}")]
    public async Task<IActionResult> IsUserOnline(int userId)
    {
        var isOnline = await _notificationService.IsUserOnlineAsync(userId);
        return Json(new { userId, isOnline });
    }

    /// <summary>
    /// Redirect to chat with a specific user (e.g., from item page or profile).
    /// </summary>
    [HttpGet]
    [Route("With/{userId}")]
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