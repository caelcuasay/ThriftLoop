// DTOs/RejectRiderDto.cs
using System.ComponentModel.DataAnnotations;

namespace ThriftLoop.DTOs.Admin;

public class RejectRiderDto
{
    [Required]
    public int RiderId { get; set; }

    [Required(ErrorMessage = "Please provide a reason for rejection.")]
    [Display(Name = "Rejection Reason")]
    [StringLength(500, MinimumLength = 10, ErrorMessage = "Rejection reason must be between 10 and 500 characters.")]
    public string Reason { get; set; } = string.Empty;
}