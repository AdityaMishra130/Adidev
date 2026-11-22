using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace CsharpProject.Models;

public sealed class AppUser : IdentityUser
{
    [Required, StringLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateOnly? DateOfBirth { get; set; }

    [StringLength(20)]
    public string? Gender { get; set; }

    [StringLength(10)]
    public string? BloodGroup { get; set; }

    [StringLength(250)]
    public string? Address { get; set; }

    [StringLength(100)]
    public string? EmergencyContactName { get; set; }

    [StringLength(30)]
    public string? EmergencyContactPhone { get; set; }

    [StringLength(500)]
    public string? Allergies { get; set; }

    [StringLength(1000)]
    public string? ExistingConditions { get; set; }

    [StringLength(300)]
    public string? ProfilePicture { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
