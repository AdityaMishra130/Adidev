using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CsharpProject.Models;

public sealed class MedicalRecord
{
    public int Id { get; set; }

    public int AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = null!;

    [StringLength(450)]
    public string? PatientId { get; set; }
    public AppUser? Patient { get; set; }

    public int? DoctorId { get; set; }
    public Doctor? Doctor { get; set; }

    [Required, StringLength(1000)]
    public string Diagnosis { get; set; } = string.Empty;

    [StringLength(4000)]
    public string Notes { get; set; } = string.Empty;

    // Vitals
    [StringLength(50)]
    public string? BloodPressure { get; set; }

    [StringLength(30)]
    public string? HeartRate { get; set; }

    [StringLength(30)]
    public string? Temperature { get; set; }

    [StringLength(30)]
    public string? SpO2 { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? WeightKg { get; set; }

    [DataType(DataType.Date)]
    public DateOnly? FollowUpDate { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();
    public ICollection<MedicalDocument> Documents { get; set; } = new List<MedicalDocument>();
}
