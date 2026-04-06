// DTOs/User/SellerApplicationDTO.cs
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using ThriftLoop.Enums;

namespace ThriftLoop.DTOs.User;

/// <summary>
/// Bound to the seller-application form submitted from Views/User/Requests.cshtml.
/// </summary>
public class SellerApplicationDTO
{
    [Required(ErrorMessage = "Shop name is required.")]
    [MaxLength(100, ErrorMessage = "Shop name cannot exceed 100 characters.")]
    [Display(Name = "Shop Name")]
    public string ShopName { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Bio cannot exceed 500 characters.")]
    [Display(Name = "Shop Bio / Tagline")]
    public string? Bio { get; set; }

    [Required(ErrorMessage = "Store address is required.")]
    [MaxLength(500, ErrorMessage = "Address cannot exceed 500 characters.")]
    [Display(Name = "Store / Pickup Address")]
    public string StoreAddress { get; set; } = string.Empty;

    /// <summary>
    /// Government-issued ID image. Required on initial submission.
    /// Accepted types are validated in the service layer (jpg, jpeg, png, pdf).
    /// </summary>
    [Required(ErrorMessage = "Please upload a government-issued ID.")]
    [Display(Name = "Government-Issued ID")]
    public IFormFile GovId { get; set; } = null!;
}

/// <summary>
/// Read-only snapshot of the user's current seller application shown on
/// Views/User/Requests.cshtml. Returned by IUserProfileService.GetSellerApplicationAsync.
/// </summary>
public class SellerApplicationStatusDTO
{
    public int Id { get; init; }
    public string ShopName { get; init; } = string.Empty;
    public string? Bio { get; init; }
    public string StoreAddress { get; init; } = string.Empty;

    /// <summary>Server-relative path to the uploaded gov-ID file.</summary>
    public string? GovIdUrl { get; init; }

    public ApplicationStatus Status { get; init; }
    public DateTime AppliedAt { get; init; }
    public DateTime? ReviewedAt { get; init; }
}