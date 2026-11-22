using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace CsharpProject.Models;

public sealed class AppointmentBookingViewModel
{
    [Display(Name = "Department")]
    public int? DepartmentId { get; set; }

    [Required(ErrorMessage = "Please select a doctor")]
    [Range(1, int.MaxValue, ErrorMessage = "Please select a valid doctor")]
    [Display(Name = "Doctor")]
    public int DoctorId { get; set; }

    [Display(Name = "Patient")]
    public string? PatientId { get; set; }

    [Required(ErrorMessage = "Appointment date and time is required")]
    [DataType(DataType.DateTime)]
    [Display(Name = "Date and Time")]
    public DateTime StartsAt { get; set; }

    [Required(ErrorMessage = "Please provide the reason for your appointment")]
    [StringLength(500, MinimumLength = 5, ErrorMessage = "Reason must be between 5 and 500 characters")]
    [Display(Name = "Reason for Visit")]
    public string Reason { get; set; } = string.Empty;
}

public sealed class AppointmentSlotDto
{
    public string TimeString { get; set; } = string.Empty;
    public string DateTimeIso { get; set; } = string.Empty;
    public bool IsAvailable { get; set; } = true;
    public string? Reason { get; set; }
}

public sealed class CreateDoctorViewModel
{
    [Required(ErrorMessage = "Doctor's name is required"), StringLength(100, MinimumLength = 2)]
    [Display(Name = "Doctor Full Name")]
    public string DisplayName { get; set; } = string.Empty;

    [Required, EmailAddress]
    [Display(Name = "Email Address")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Department")]
    public int? DepartmentId { get; set; }

    [Required(ErrorMessage = "Specialty is required"), StringLength(80)]
    [Display(Name = "Specialty / Sub-specialty")]
    public string Specialty { get; set; } = string.Empty;

    [Required(ErrorMessage = "Medical qualifications are required"), StringLength(200)]
    [Display(Name = "Qualifications (e.g., MBBS, MD, MS)")]
    public string Qualification { get; set; } = "MBBS, MD";

    [Range(0, 70)]
    [Display(Name = "Years of Experience")]
    public int ExperienceYears { get; set; } = 5;

    [Range(0, 100000)]
    [Display(Name = "Consultation Fee (INR)")]
    public decimal ConsultationFee { get; set; } = 500.00m;

    [Required, StringLength(50)]
    [Display(Name = "Room / OPD Number")]
    public string RoomNumber { get; set; } = "OPD-101";

    [StringLength(1000)]
    [Display(Name = "Professional Bio / Summary")]
    public string? Bio { get; set; }

    [Required, StringLength(128, MinimumLength = 8)]
    [DataType(DataType.Password)]
    [Display(Name = "Temporary Password")]
    public string Password { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Available Weekdays (comma-separated)")]
    public string AvailableDays { get; set; } = "Monday,Tuesday,Wednesday,Thursday,Friday";

    [Display(Name = "Clinic Opens")]
    public TimeOnly StartTime { get; set; } = new(9, 0);

    [Display(Name = "Clinic Closes")]
    public TimeOnly EndTime { get; set; } = new(17, 0);
}

public sealed class EditDoctorViewModel
{
    public int Id { get; set; }

    [Required, StringLength(100, MinimumLength = 2)]
    [Display(Name = "Doctor Full Name")]
    public string DisplayName { get; set; } = string.Empty;

    [Display(Name = "Department")]
    public int? DepartmentId { get; set; }

    [Required, StringLength(80)]
    [Display(Name = "Specialty")]
    public string Specialty { get; set; } = string.Empty;

    [Required, StringLength(200)]
    [Display(Name = "Qualifications")]
    public string Qualification { get; set; } = string.Empty;

    [Range(0, 70)]
    [Display(Name = "Experience (Years)")]
    public int ExperienceYears { get; set; }

    [Range(0, 100000)]
    [Display(Name = "Consultation Fee (INR)")]
    public decimal ConsultationFee { get; set; }

    [Required, StringLength(50)]
    [Display(Name = "Room / OPD Number")]
    public string RoomNumber { get; set; } = string.Empty;

    [StringLength(1000)]
    [Display(Name = "Professional Bio")]
    public string? Bio { get; set; }

    [Required, StringLength(100)]
    [Display(Name = "Available Weekdays")]
    public string AvailableDays { get; set; } = string.Empty;

    [Display(Name = "Clinic Opens")]
    public TimeOnly StartTime { get; set; }

    [Display(Name = "Clinic Closes")]
    public TimeOnly EndTime { get; set; }

    [Display(Name = "Active Practitioner")]
    public bool IsActive { get; set; } = true;
}

public sealed class DoctorScheduleViewModel
{
    [Required, StringLength(100)]
    [Display(Name = "Available Weekdays (comma-separated)")]
    public string AvailableDays { get; set; } = string.Empty;

    [Display(Name = "Clinic Opens")]
    public TimeOnly StartTime { get; set; }

    [Display(Name = "Clinic Closes")]
    public TimeOnly EndTime { get; set; }

    [Display(Name = "Consultation Fee (INR)")]
    public decimal ConsultationFee { get; set; }

    [Display(Name = "Room / OPD Number")]
    public string RoomNumber { get; set; } = string.Empty;

    public string Specialty { get; set; } = string.Empty;
}

public sealed class PrescriptionItemViewModel
{
    [Required(ErrorMessage = "Medication name is required"), StringLength(150)]
    [Display(Name = "Medication Name")]
    public string Medication { get; set; } = string.Empty;

    [Required(ErrorMessage = "Dosage is required"), StringLength(100)]
    [Display(Name = "Dosage (e.g. 500mg, 10ml)")]
    public string Dosage { get; set; } = string.Empty;

    [StringLength(50)]
    [Display(Name = "Frequency")]
    public string Frequency { get; set; } = "1-0-1 (Twice daily)";

    [StringLength(50)]
    [Display(Name = "Duration")]
    public string Duration { get; set; } = "5 days";

    [StringLength(50)]
    [Display(Name = "Meal Timing")]
    public string MealTiming { get; set; } = "After food";

    [StringLength(500)]
    [Display(Name = "Specific Instructions")]
    public string Instructions { get; set; } = string.Empty;
}

public sealed class CreateMedicalRecordViewModel
{
    public int AppointmentId { get; set; }
    public string? PatientName { get; set; }
    public string? DoctorName { get; set; }
    public DateTime AppointmentTime { get; set; }

    [Required(ErrorMessage = "Clinical diagnosis is required"), StringLength(1000)]
    [Display(Name = "Clinical Diagnosis")]
    public string Diagnosis { get; set; } = string.Empty;

    [StringLength(4000)]
    [Display(Name = "Clinical Notes / Observations")]
    public string Notes { get; set; } = string.Empty;

    // Vitals
    [StringLength(50)]
    [Display(Name = "Blood Pressure (e.g. 120/80 mmHg)")]
    public string? BloodPressure { get; set; }

    [StringLength(30)]
    [Display(Name = "Heart Rate (bpm)")]
    public string? HeartRate { get; set; }

    [StringLength(30)]
    [Display(Name = "Temperature (°F)")]
    public string? Temperature { get; set; }

    [StringLength(30)]
    [Display(Name = "Oxygen Saturation (SpO2 %)")]
    public string? SpO2 { get; set; }

    [Range(0, 500)]
    [Display(Name = "Weight (kg)")]
    public decimal? WeightKg { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Follow-up Recommended Date")]
    public DateOnly? FollowUpDate { get; set; }

    // Primary Prescription
    [Required(ErrorMessage = "Medication name is required"), StringLength(150)]
    public string Medication { get; set; } = string.Empty;

    [Required(ErrorMessage = "Dosage is required"), StringLength(100)]
    public string Dosage { get; set; } = string.Empty;

    public string Frequency { get; set; } = "1-0-1 (Twice daily)";
    public string Duration { get; set; } = "5 days";
    public string MealTiming { get; set; } = "After food";
    public string Instructions { get; set; } = string.Empty;

    // Additional Prescriptions (JSON or multiple)
    public List<PrescriptionItemViewModel> AdditionalMedicines { get; set; } = new();
}

public sealed class MedicalDocumentUploadViewModel
{
    public int? MedicalRecordId { get; set; }

    [Required, StringLength(450)]
    public string PatientId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Document title is required"), StringLength(200)]
    [Display(Name = "Document Title (e.g., Blood Test Report, MRI Scan)")]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(50)]
    [Display(Name = "Document Category")]
    public string DocumentType { get; set; } = "Lab Report";

    [Required(ErrorMessage = "Please select a file to upload")]
    [Display(Name = "Upload File (PDF, PNG, JPG - Max 10MB)")]
    public IFormFile File { get; set; } = null!;
}

public sealed class UserProfileViewModel
{
    public string Id { get; set; } = string.Empty;

    [Required, StringLength(100)]
    [Display(Name = "Full Name")]
    public string DisplayName { get; set; } = string.Empty;

    [EmailAddress]
    [Display(Name = "Email Address")]
    public string Email { get; set; } = string.Empty;

    [Phone]
    [Display(Name = "Phone Number")]
    public string? PhoneNumber { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Date of Birth")]
    public DateOnly? DateOfBirth { get; set; }

    [Display(Name = "Gender")]
    public string? Gender { get; set; }

    [Display(Name = "Blood Group")]
    public string? BloodGroup { get; set; }

    [StringLength(250)]
    [Display(Name = "Residential Address")]
    public string? Address { get; set; }

    [StringLength(100)]
    [Display(Name = "Emergency Contact Name")]
    public string? EmergencyContactName { get; set; }

    [Phone]
    [Display(Name = "Emergency Contact Phone")]
    public string? EmergencyContactPhone { get; set; }

    [StringLength(500)]
    [Display(Name = "Known Allergies")]
    public string? Allergies { get; set; }

    [StringLength(1000)]
    [Display(Name = "Existing Medical Conditions / Chronic Illnesses")]
    public string? ExistingConditions { get; set; }
}

public sealed class AdminDashboardViewModel
{
    public int TotalPatients { get; set; }
    public int TotalDoctors { get; set; }
    public int TotalAppointments { get; set; }
    public int PendingAppointments { get; set; }
    public int ConfirmedAppointments { get; set; }
    public int CompletedAppointments { get; set; }
    public int CancelledAppointments { get; set; }
    public int TotalDepartments { get; set; }
    public int TotalMedicalRecords { get; set; }

    public List<DepartmentStatDto> DepartmentStats { get; set; } = new();
    public List<Appointment> RecentAppointments { get; set; } = new();
    public List<Doctor> Doctors { get; set; } = new();
    public List<AppUser> RecentPatients { get; set; } = new();
}

public sealed class DepartmentStatDto
{
    public string DepartmentName { get; set; } = string.Empty;
    public int DoctorCount { get; set; }
    public int AppointmentCount { get; set; }
}

public sealed class DoctorDashboardViewModel
{
    public Doctor Doctor { get; set; } = null!;
    public List<Appointment> TodayAppointments { get; set; } = new();
    public List<Appointment> PendingRequests { get; set; } = new();
    public List<Appointment> UpcomingAppointments { get; set; } = new();
    public int CompletedCount { get; set; }
    public int TotalPatientCount { get; set; }
}

public sealed class PatientDashboardViewModel
{
    public AppUser Patient { get; set; } = null!;
    public List<Appointment> UpcomingAppointments { get; set; } = new();
    public List<Appointment> PastAppointments { get; set; } = new();
    public List<MedicalRecord> RecentRecords { get; set; } = new();
    public List<MedicalDocument> Documents { get; set; } = new();
}
