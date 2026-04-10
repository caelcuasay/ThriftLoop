// DTOs/RiderEditDTO.cs
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ThriftLoop.DTOs.Auth;

public class RiderEditDTO
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Full name is required.")]
    [Display(Name = "Full Name")]
    [StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress]
    [Display(Name = "Email Address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone number is required.")]
    [Phone]
    [Display(Name = "Phone Number")]
    public string PhoneNumber { get; set; } = string.Empty;

    // Optional: Password change fields
    [DataType(DataType.Password)]
    [MinLength(8)]
    [Display(Name = "New Password (optional)")]
    public string? NewPassword { get; set; }

    [DataType(DataType.Password)]
    [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm New Password")]
    public string? ConfirmNewPassword { get; set; }

    // Image upload - optional for edit
    [Display(Name = "Driver's License Photo (upload new if needed)")]
    public IFormFile? DriversLicense { get; set; }

    [Required(ErrorMessage = "Vehicle type is required.")]
    [Display(Name = "Vehicle Type")]
    [StringLength(50)]
    public string VehicleType { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vehicle color is required.")]
    [Display(Name = "Vehicle Color")]
    [StringLength(30)]
    public string VehicleColor { get; set; } = string.Empty;

    [Required(ErrorMessage = "License plate is required.")]
    [Display(Name = "License Plate")]
    [StringLength(15, MinimumLength = 2)]
    public string LicensePlate { get; set; } = string.Empty;

    [Required(ErrorMessage = "Address is required.")]
    [Display(Name = "Home Address")]
    [StringLength(250, MinimumLength = 10)]
    public string Address { get; set; } = string.Empty;

    // Store existing license URL for display
    public string? ExistingLicenseUrl { get; set; }
}