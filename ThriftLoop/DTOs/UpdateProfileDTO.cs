// DTOs/User/UpdateProfileDTO.cs
using System.ComponentModel.DataAnnotations;

namespace ThriftLoop.DTOs.User;

/// <summary>
/// Fields the user can edit on their profile page.
/// </summary>
public class UpdateProfileDTO
{
    [Display(Name = "Full Name")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string? FullName { get; set; }

    [Display(Name = "Phone Number")]
    [Phone(ErrorMessage = "Enter a valid phone number.")]
    [MaxLength(20, ErrorMessage = "Phone number cannot exceed 20 characters.")]
    public string? PhoneNumber { get; set; }

    [Display(Name = "Address")]
    [MaxLength(300, ErrorMessage = "Address cannot exceed 300 characters.")]
    public string? Address { get; set; }

    // Optional coordinates saved when the user picks a point on the map
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}