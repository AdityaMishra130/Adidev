using System.ComponentModel.DataAnnotations;

namespace CsharpProject.Models;

public sealed class Appointment
{
    public int Id { get; set; }

    [Required]
    public string PatientId { get; set; } = string.Empty;
    public AppUser Patient { get; set; } = null!;

    public int DoctorId { get; set; }
    public Doctor Doctor { get; set; } = null!;

    public DateTime StartsAt { get; set; }

    [Required, StringLength(500)]
    public string Reason { get; set; } = string.Empty;

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;

    [StringLength(1000)]
    public string? DoctorNotes { get; set; }

    [StringLength(500)]
    public string? RejectionReason { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public MedicalRecord? MedicalRecord { get; set; }
}

public enum AppointmentStatus
{
    Pending = 0,
    Scheduled = 0,
    Confirmed = 1,
    Rejected = 2,
    Cancelled = 3,
    Completed = 4
}
