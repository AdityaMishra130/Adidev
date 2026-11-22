using System.Security.Claims;
using CsharpProject.Data;
using CsharpProject.Models;
using CsharpProject.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CsharpProject.Controllers;

[Authorize]
public sealed class RecordsController(
    PortalDbContext database,
    INotificationService notificationService) : Controller
{
    public async Task<IActionResult> Index(string? search)
    {
        var query = database.MedicalRecords
            .Include(record => record.Appointment).ThenInclude(appointment => appointment.Patient)
            .Include(record => record.Appointment).ThenInclude(appointment => appointment.Doctor).ThenInclude(doctor => doctor.User)
            .Include(record => record.Prescriptions)
            .Include(record => record.Documents)
            .AsQueryable();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (User.IsInRole(PortalRoles.Patient))
        {
            query = query.Where(record => record.PatientId == currentUserId || record.Appointment.PatientId == currentUserId);
        }
        else if (User.IsInRole(PortalRoles.Doctor))
        {
            query = query.Where(record => (record.Doctor != null && record.Doctor.UserId == currentUserId) || record.Appointment.Doctor.UserId == currentUserId);
        }
        else if (!User.IsInRole(PortalRoles.Admin))
        {
            return Forbid();
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(r =>
                r.Diagnosis.ToLower().Contains(s) ||
                (r.Patient != null && r.Patient.DisplayName.ToLower().Contains(s)) ||
                (r.Appointment.Patient.DisplayName.ToLower().Contains(s)) ||
                r.Prescriptions.Any(p => p.Medication.ToLower().Contains(s)));
            ViewBag.SearchTerm = search;
        }

        return View(await query.OrderByDescending(record => record.CreatedAtUtc).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var record = await database.MedicalRecords
            .Include(r => r.Appointment).ThenInclude(a => a.Patient)
            .Include(r => r.Appointment).ThenInclude(a => a.Doctor).ThenInclude(d => d.User)
            .Include(r => r.Appointment).ThenInclude(a => a.Doctor).ThenInclude(d => d.Department)
            .Include(r => r.Patient)
            .Include(r => r.Doctor).ThenInclude(d => d!.User)
            .Include(r => r.Prescriptions)
            .Include(r => r.Documents)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (record is null) return NotFound();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var patientId = record.PatientId ?? record.Appointment.PatientId;
        var doctorUserId = record.Doctor?.UserId ?? record.Appointment.Doctor.UserId;

        if (!User.IsInRole(PortalRoles.Admin) &&
            patientId != currentUserId &&
            doctorUserId != currentUserId)
        {
            return Forbid();
        }

        return View(record);
    }

    [Authorize(Roles = PortalRoles.Doctor)]
    public async Task<IActionResult> Create(int appointmentId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var appointment = await database.Appointments
            .Include(item => item.Doctor)
            .Include(item => item.Patient)
            .SingleOrDefaultAsync(item => item.Id == appointmentId && item.Doctor.UserId == userId);

        if (appointment is null) return NotFound();

        if (await database.MedicalRecords.AnyAsync(record => record.AppointmentId == appointmentId))
        {
            TempData["Error"] = "A clinical consultation record has already been created for this appointment.";
            return RedirectToAction(nameof(Details), new { id = (await database.MedicalRecords.FirstAsync(r => r.AppointmentId == appointmentId)).Id });
        }

        var vm = new CreateMedicalRecordViewModel
        {
            AppointmentId = appointmentId,
            PatientName = appointment.Patient.DisplayName,
            DoctorName = User.Identity?.Name,
            AppointmentTime = appointment.StartsAt
        };

        return View(vm);
    }

    [HttpPost]
    [Authorize(Roles = PortalRoles.Doctor)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateMedicalRecordViewModel model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var appointment = await database.Appointments
            .Include(item => item.Doctor).ThenInclude(d => d.User)
            .Include(item => item.Patient)
            .SingleOrDefaultAsync(item => item.Id == model.AppointmentId && item.Doctor.UserId == userId);

        if (appointment is null) return NotFound();

        if (await database.MedicalRecords.AnyAsync(record => record.AppointmentId == model.AppointmentId))
        {
            ModelState.AddModelError(string.Empty, "A clinical record already exists for this appointment.");
        }

        if (string.IsNullOrWhiteSpace(model.Diagnosis))
        {
            ModelState.AddModelError(nameof(model.Diagnosis), "Clinical diagnosis is required.");
        }

        if (string.IsNullOrWhiteSpace(model.Medication) || string.IsNullOrWhiteSpace(model.Dosage))
        {
            ModelState.AddModelError(nameof(model.Medication), "Please enter primary medication and dosage.");
        }

        if (!ModelState.IsValid)
        {
            model.PatientName = appointment.Patient.DisplayName;
            model.DoctorName = appointment.Doctor.User.DisplayName;
            model.AppointmentTime = appointment.StartsAt;
            return View(model);
        }

        var record = new MedicalRecord
        {
            AppointmentId = appointment.Id,
            PatientId = appointment.PatientId,
            DoctorId = appointment.DoctorId,
            Diagnosis = model.Diagnosis.Trim(),
            Notes = model.Notes?.Trim() ?? string.Empty,
            BloodPressure = model.BloodPressure?.Trim(),
            HeartRate = model.HeartRate?.Trim(),
            Temperature = model.Temperature?.Trim(),
            SpO2 = model.SpO2?.Trim(),
            WeightKg = model.WeightKg,
            FollowUpDate = model.FollowUpDate,
            CreatedAtUtc = DateTime.UtcNow
        };

        // Primary medication
        record.Prescriptions.Add(new Prescription
        {
            Medication = model.Medication.Trim(),
            Dosage = model.Dosage.Trim(),
            Frequency = model.Frequency?.Trim() ?? "1-0-1 (Twice daily)",
            Duration = model.Duration?.Trim() ?? "5 days",
            MealTiming = model.MealTiming?.Trim() ?? "After food",
            Instructions = model.Instructions?.Trim() ?? string.Empty
        });

        // Additional medications if provided
        if (model.AdditionalMedicines != null)
        {
            foreach (var med in model.AdditionalMedicines.Where(m => !string.IsNullOrWhiteSpace(m.Medication)))
            {
                record.Prescriptions.Add(new Prescription
                {
                    Medication = med.Medication.Trim(),
                    Dosage = med.Dosage?.Trim() ?? "As advised",
                    Frequency = med.Frequency?.Trim() ?? "1-0-1",
                    Duration = med.Duration?.Trim() ?? "5 days",
                    MealTiming = med.MealTiming?.Trim() ?? "After food",
                    Instructions = med.Instructions?.Trim() ?? string.Empty
                });
            }
        }

        database.MedicalRecords.Add(record);

        // Ensure appointment status is marked Completed
        if (appointment.Status != AppointmentStatus.Completed)
        {
            appointment.Status = AppointmentStatus.Completed;
            appointment.UpdatedAtUtc = DateTime.UtcNow;
        }

        await database.SaveChangesAsync();

        // Notify patient
        await notificationService.SendNotificationAsync(
            appointment.PatientId,
            "Consultation Record & Prescription Added",
            $"Dr. {appointment.Doctor.User.DisplayName} has published your clinical visit notes and prescription.",
            "MedicalRecord",
            $"/Records/Details/{record.Id}");

        TempData["Notice"] = "Medical consultation record and prescription saved successfully.";
        return RedirectToAction(nameof(Details), new { id = record.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Print(int id)
    {
        var record = await database.MedicalRecords
            .Include(r => r.Appointment).ThenInclude(a => a.Patient)
            .Include(r => r.Appointment).ThenInclude(a => a.Doctor).ThenInclude(d => d.User)
            .Include(r => r.Appointment).ThenInclude(a => a.Doctor).ThenInclude(d => d.Department)
            .Include(r => r.Patient)
            .Include(r => r.Doctor).ThenInclude(d => d!.User)
            .Include(r => r.Doctor).ThenInclude(d => d!.Department)
            .Include(r => r.Prescriptions)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (record is null) return NotFound();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var patientId = record.PatientId ?? record.Appointment.PatientId;
        var doctorUserId = record.Doctor?.UserId ?? record.Appointment.Doctor.UserId;

        if (!User.IsInRole(PortalRoles.Admin) &&
            patientId != currentUserId &&
            doctorUserId != currentUserId)
        {
            return Forbid();
        }

        return View(record);
    }
}
