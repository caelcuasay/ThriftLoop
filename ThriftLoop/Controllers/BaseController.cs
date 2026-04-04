using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ThriftLoop.Controllers;

/// <summary>
/// Shared base for all ThriftLoop controllers.
/// Centralises cross-cutting helpers so they are never duplicated.
/// </summary>
public abstract class BaseController : Controller
{
    /// <summary>
    /// Returns the authenticated user's ID parsed from the NameIdentifier claim,
    /// or <c>null</c> for anonymous requests.
    /// Works for both regular Users and Riders — both store their primary key
    /// in the same NameIdentifier claim.
    /// </summary>
    protected int? ResolveUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out int id) ? id : null;
    }

    /// <summary>
    /// Returns the authenticated user's ID, or throws <see cref="UnauthorizedAccessException"/>
    /// if the claim is missing. Use in [Authorize] actions where null is never expected.
    /// </summary>
    protected int RequireUserId()
        => ResolveUserId() ?? throw new UnauthorizedAccessException("User is not authenticated.");

    /// <summary>
    /// Returns <c>true</c> when the currently authenticated principal is a Rider.
    /// Matches the "IsRider" = "true" custom claim stamped by
    /// AuthController.SignInRiderAsync. Regular Users always have "IsRider" = "false".
    /// </summary>
    protected bool IsRider()
        => User.FindFirstValue("IsRider") == "true";
}