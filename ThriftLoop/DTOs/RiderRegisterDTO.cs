using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ThriftLoop.DTOs.Auth;

public class RiderRegisterDTO
{
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

    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    [MinLength(8)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    // ── Image upload — replaces the text license number field ──
    [Required(ErrorMessage = "A photo of your driver's license is required.")]
    [Display(Name = "Driver's License Photo")]
    public IFormFile DriversLicense { get; set; } = null!;

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
}