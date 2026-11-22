using System.ComponentModel.DataAnnotations;

namespace CsharpProject.Models;

public sealed class MedicalDocument
{
    public int Id { get; set; }

    public int? MedicalRecordId { get; set; }
    public MedicalRecord? MedicalRecord { get; set; }

    [Required, StringLength(450)]
    public string PatientId { get; set; } = string.Empty;
    public AppUser Patient { get; set; } = null!;

    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string DocumentType { get; set; } = "Lab Report";

    [Required, StringLength(260)]
    public string FileName { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string FilePath { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    [StringLength(100)]
    public string ContentType { get; set; } = "application/pdf";

    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
}
