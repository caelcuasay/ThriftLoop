using ThriftLoop.Enums;

namespace ThriftLoop.Models;

/// <summary>
/// Created when a User submits a Seller application.
/// Holds shop branding data and tracks where the application sits in the
/// admin review pipeline. A User can have at most one SellerProfile
/// (enforced by a unique index on UserId in the DbContext).
///
/// Flow:
///   User submits application → ApplicationStatus = Pending
///   Admin approves           → ApplicationStatus = Approved, User.Role = Seller
///   Admin rejects            → ApplicationStatus = Rejected, User.Role unchanged
/// </summary>
public class SellerProfile
{
    public int Id { get; set; }

    /// <summary>FK → Users.Id — the user who owns this shop.</summary>
    public int UserId { get; set; }

    // ── Application state ─────────────────────────────────────────────────────

    /// <summary>
    /// Where this seller application currently sits.
    /// User.Role is only set to Seller once this reaches Approved.
    /// </summary>
    public ApplicationStatus ApplicationStatus { get; set; } = ApplicationStatus.Pending;

    /// <summary>UTC timestamp when the application was submitted.</summary>
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// UTC timestamp when an admin took action on the application.
    /// Null while the application is still Pending.
    /// </summary>
    public DateTime? ReviewedAt { get; set; }

    // ── Application details ───────────────────────────────────────────────────

    /// <summary>
    /// Physical store / pickup address supplied during the application.
    /// Required so the admin can verify the applicant operates a real location.
    /// </summary>
    public string StoreAddress { get; set; } = string.Empty;

    /// <summary>
    /// Server-relative path to the uploaded government-issued ID image,
    /// e.g. "/uploads/gov-ids/42_20240101_passport.jpg".
    /// Stored by the application; displayed to admins during review.
    /// </summary>
    public string? GovIdUrl { get; set; }

    // ── Shop branding ─────────────────────────────────────────────────────────
    // These fields are filled in by the user during application and can be
    // edited later from the shop settings page once approved.

    /// <summary>Public-facing shop name shown on the shop page and item listings.</summary>
    public string ShopName { get; set; } = string.Empty;

    /// <summary>Short description or tagline shown on the shop page.</summary>
    public string? Bio { get; set; }

    /// <summary>Path or URL to the shop banner image.</summary>
    public string? BannerUrl { get; set; }

    /// <summary>Path or URL to the shop logo/avatar image.</summary>
    public string? LogoUrl { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────────

    public User? User { get; set; }

    /// <summary>All shop listings posted under this profile.</summary>
    public ICollection<Item> Items { get; set; } = new List<Item>();
}