using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CsharpProject.Models;

public sealed class Doctor
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public AppUser User { get; set; } = null!;

    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    [Required, StringLength(80)]
    public string Specialty { get; set; } = string.Empty;

    [StringLength(200)]
    public string Qualification { get; set; } = "MBBS, MD";

    public int ExperienceYears { get; set; } = 5;

    [Column(TypeName = "decimal(18,2)")]
    public decimal ConsultationFee { get; set; } = 500.00m;

    [StringLength(50)]
    public string RoomNumber { get; set; } = "OPD-101";

    [StringLength(1000)]
    public string? Bio { get; set; }

    public bool IsActive { get; set; } = true;

    [Required, StringLength(100)]
    public string AvailableDays { get; set; } = "Monday,Tuesday,Wednesday,Thursday,Friday";

    public TimeOnly StartTime { get; set; } = new(9, 0);
    public TimeOnly EndTime { get; set; } = new(17, 0);

    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
