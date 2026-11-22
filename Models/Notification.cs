using System.ComponentModel.DataAnnotations;

namespace CsharpProject.Models;

public sealed class Notification
{
    public int Id { get; set; }

    [Required, StringLength(450)]
    public string UserId { get; set; } = string.Empty;
    public AppUser User { get; set; } = null!;

    [Required, StringLength(150)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string Message { get; set; } = string.Empty;

    [StringLength(50)]
    public string Type { get; set; } = "Appointment";

    public bool IsRead { get; set; } = false;

    [StringLength(300)]
    public string? RelatedUrl { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
