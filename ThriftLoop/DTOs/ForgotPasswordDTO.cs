using System.ComponentModel.DataAnnotations;

namespace ThriftLoop.DTOs.Auth;

public class ForgotPasswordDTO
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [Display(Name = "Email Address")]
    public string Email { get; set; } = string.Empty;
}