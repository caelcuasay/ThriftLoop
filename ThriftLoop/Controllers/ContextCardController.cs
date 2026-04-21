// Controllers/ContextCardController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.IO;
using ThriftLoop.DTOs.Chat;
using ThriftLoop.Enums;
using ThriftLoop.Services.Interface;

namespace ThriftLoop.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ContextCardController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly ILogger<ContextCardController> _logger;

    public ContextCardController(IChatService chatService, ILogger<ContextCardController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    /// <summary>
    /// Updates a context card with the specified action.
    /// </summary>
    /// <param name="contextCardId">The ID of the context card.</param>
    /// <param name="request">The update request containing action and optional payment method.</param>
    /// <returns>The updated context card.</returns>
    [HttpPut("{contextCardId}")]
    public async Task<ActionResult<ContextCardDTO>> UpdateContextCard(int contextCardId, [FromBody] UpdateContextCardRequest request)
    {
        try
        {
            // Log the raw request body for debugging
            using var reader = new StreamReader(Request.Body);
            var requestBody = await reader.ReadToEndAsync();
            _logger.LogInformation("Raw request body: {RequestBody}", requestBody);
            
            _logger.LogInformation("UpdateContextCard called with ID: {ContextCardId}, Action: {Action}, PaymentMethod: {PaymentMethod}", 
                contextCardId, request.Action, request.PaymentMethod);
            
            var currentUserId = GetCurrentUserId();
            var updatedCard = await _chatService.UpdateContextCardAsync(
                contextCardId, 
                request.Action, 
                currentUserId, 
                request.PaymentMethod);

            return Ok(updatedCard);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Context card not found: {ContextCardId}", contextCardId);
            return NotFound("Context card not found.");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to context card: {ContextCardId}", contextCardId);
            return StatusCode(403, "You are not authorized to perform this action.");
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument for context card update: {ContextCardId} - {Message}", contextCardId, ex.Message);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating context card: {ContextCardId} - {Type}: {ExceptionType} - {Message}", 
                contextCardId, ex.GetType().Name, ex.Message);
            return StatusCode(500, "An error occurred while updating the context card.");
        }
    }

    /// <summary>
    /// Gets all context cards for a conversation.
    /// </summary>
    /// <param name="conversationId">The conversation ID.</param>
    /// <returns>List of context cards.</returns>
    [HttpGet("conversation/{conversationId}")]
    public async Task<ActionResult<List<ContextCardDTO>>> GetContextCards(int conversationId)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            
            // Check if user can access the conversation
            if (!await _chatService.CanAccessConversationAsync(conversationId, currentUserId))
            {
                return StatusCode(403, "You are not a participant in this conversation.");
            }

            var contextCards = await _chatService.GetContextCardsAsync(conversationId, currentUserId);
            return Ok(contextCards);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting context cards for conversation: {ConversationId}", conversationId);
            return StatusCode(500, "An error occurred while retrieving context cards.");
        }
    }

    /// <summary>
    /// Creates a new context card for an item inquiry.
    /// </summary>
    /// <param name="request">The create context card request.</param>
    /// <returns>The created context card.</returns>
    [HttpPost]
    public async Task<ActionResult<ContextCardDTO>> CreateContextCard([FromBody] CreateContextCardRequest request)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            
            // Check if user can access the conversation
            if (!await _chatService.CanAccessConversationAsync(request.ConversationId, currentUserId))
            {
                return StatusCode(403, "You are not a participant in this conversation.");
            }

            var contextCard = await _chatService.CreateContextCardAsync(
                request.ConversationId, 
                request.ItemId, 
                request.BuyerId, 
                request.SellerId);

            return CreatedAtAction(nameof(GetContextCards), new { conversationId = request.ConversationId }, contextCard);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation creating context card");
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating context card");
            return StatusCode(500, "An error occurred while creating the context card.");
        }
    }

    /// <summary>
    /// Processes expired context cards (admin operation).
    /// </summary>
    /// <returns>Result of the operation.</returns>
    [HttpPost("process-expired")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> ProcessExpiredContextCards()
    {
        try
        {
            await _chatService.ProcessExpiredContextCardsAsync();
            return Ok("Expired context cards processed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing expired context cards");
            return StatusCode(500, "An error occurred while processing expired context cards.");
        }
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            throw new UnauthorizedAccessException("User ID not found or invalid.");
        }
        return userId;
    }
}

/// <summary>
/// Request model for updating a context card.
/// </summary>
public class UpdateContextCardRequest
{
    [Required]
    public ContextCardAction Action { get; set; }
    
    public ThriftLoop.DTOs.Chat.PaymentMethod? PaymentMethod { get; set; }
}

/// <summary>
/// Request model for creating a context card.
/// </summary>
public class CreateContextCardRequest
{
    public int ConversationId { get; set; }
    public int ItemId { get; set; }
    public int BuyerId { get; set; }
    public int SellerId { get; set; }
}
