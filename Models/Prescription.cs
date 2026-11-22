using System.ComponentModel.DataAnnotations;

namespace CsharpProject.Models;

public sealed class Prescription
{
    public int Id { get; set; }

    public int MedicalRecordId { get; set; }
    public MedicalRecord MedicalRecord { get; set; } = null!;

    [Required, StringLength(150)]
    public string Medication { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Dosage { get; set; } = string.Empty;

    [StringLength(50)]
    public string Frequency { get; set; } = "1-0-1 (Twice daily)";

    [StringLength(50)]
    public string Duration { get; set; } = "5 days";

    [StringLength(50)]
    public string MealTiming { get; set; } = "After food";

    [StringLength(500)]
    public string Instructions { get; set; } = string.Empty;
}
