using System.Security.Claims;
using CsharpProject.Data;
using CsharpProject.Models;
using CsharpProject.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CsharpProject.Controllers;

[Authorize]
public sealed class AppointmentsController(
    PortalDbContext database,
    INotificationService notificationService) : Controller
{
    public async Task<IActionResult> Index(int? status, string? search, int? doctorId)
    {
        ViewBag.LastAppointmentId = HttpContext.Session.GetInt32("LastAppointmentId");
        HttpContext.Session.Remove("LastAppointmentId");

        var query = database.Appointments
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Include(a => a.Doctor).ThenInclude(d => d.Department)
            .Include(a => a.Patient)
            .Include(a => a.MedicalRecord)
            .AsQueryable();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (User.IsInRole(PortalRoles.Patient))
        {
            query = query.Where(a => a.PatientId == currentUserId);
        }
        else if (User.IsInRole(PortalRoles.Doctor))
        {
            query = query.Where(a => a.Doctor.UserId == currentUserId);
        }
        else if (!User.IsInRole(PortalRoles.Admin))
        {
            return Forbid();
        }

        if (status.HasValue && Enum.IsDefined(typeof(AppointmentStatus), status.Value))
        {
            var s = (AppointmentStatus)status.Value;
            query = query.Where(a => a.Status == s);
            ViewBag.SelectedStatus = status.Value;
        }

        if (doctorId.HasValue && doctorId.Value > 0)
        {
            query = query.Where(a => a.DoctorId == doctorId.Value);
            ViewBag.SelectedDoctorId = doctorId.Value;
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(a =>
                a.Reason.ToLower().Contains(term) ||
                a.Patient.DisplayName.ToLower().Contains(term) ||
                a.Doctor.User.DisplayName.ToLower().Contains(term));
            ViewBag.SearchTerm = search;
        }

        ViewBag.Doctors = await database.Doctors
            .Where(d => d.IsActive)
            .Select(d => new { d.Id, Name = d.User.DisplayName })
            .ToListAsync();

        return View(await query.OrderByDescending(a => a.StartsAt).ToListAsync());
    }

    [Authorize]
    public async Task<IActionResult> Create(int? doctorId)
    {
        await PopulateDepartmentsAndDoctors(doctorId);
        var initialModel = new AppointmentBookingViewModel
        {
            DoctorId = doctorId ?? 0,
            StartsAt = DateTime.Now.AddDays(1).Date.AddHours(9)
        };

        if (doctorId.HasValue && doctorId.Value > 0)
        {
            var doc = await database.Doctors.FindAsync(doctorId.Value);
            if (doc != null)
            {
                initialModel.DepartmentId = doc.DepartmentId;
            }
        }

        return View(initialModel);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AppointmentBookingViewModel model)
    {
        var doctor = await database.Doctors
            .Include(item => item.User)
            .Include(item => item.Department)
            .SingleOrDefaultAsync(item => item.Id == model.DoctorId && item.IsActive);

        if (doctor is null)
        {
            ModelState.AddModelError(nameof(model.DoctorId), "Please select an active, verified doctor.");
        }
        else if (model.StartsAt <= DateTime.Now)
        {
            ModelState.AddModelError(nameof(model.StartsAt), "Choose a future appointment time.");
        }
        else if (string.IsNullOrWhiteSpace(model.Reason))
        {
            ModelState.AddModelError(nameof(model.Reason), "Enter the reason for your medical visit.");
        }
        else
        {
            var allowedDays = doctor.AvailableDays.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (!allowedDays.Contains(model.StartsAt.DayOfWeek.ToString(), StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(model.StartsAt), $"Dr. {doctor.User.DisplayName} is not available on {model.StartsAt.DayOfWeek}s.");
            }

            var requestedTime = TimeOnly.FromDateTime(model.StartsAt);
            var slotEnd = requestedTime.AddMinutes(30);
            if (requestedTime.Minute % 30 != 0 || requestedTime.Second != 0 ||
                requestedTime < doctor.StartTime || slotEnd > doctor.EndTime)
            {
                ModelState.AddModelError(nameof(model.StartsAt), $"Choose a 30-minute slot within clinic hours ({doctor.StartTime:hh\\:mm} - {doctor.EndTime:hh\\:mm}).");
            }

            var slot = model.StartsAt;
            var isTaken = await database.Appointments.AnyAsync(appointment =>
                appointment.DoctorId == doctor.Id &&
                (appointment.Status == AppointmentStatus.Pending || appointment.Status == AppointmentStatus.Confirmed) &&
                appointment.StartsAt == slot);

            if (isTaken)
            {
                ModelState.AddModelError(nameof(model.StartsAt), "That appointment slot has already been booked. Please pick another time.");
            }
        }

        if (!ModelState.IsValid)
        {
            await PopulateDepartmentsAndDoctors(model.DoctorId, model.DepartmentId);
            return View(model);
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var targetPatientId = currentUserId;

        if ((User.IsInRole(PortalRoles.Admin) || User.IsInRole(PortalRoles.Doctor)) && !string.IsNullOrWhiteSpace(model.PatientId))
        {
            targetPatientId = model.PatientId;
        }

        var patientUser = await database.Users.FindAsync(targetPatientId);
        var patientName = patientUser?.DisplayName ?? User.Identity?.Name ?? "Patient";

        var appointment = new Appointment
        {
            DoctorId = doctor!.Id,
            PatientId = targetPatientId,
            StartsAt = model.StartsAt,
            Reason = model.Reason.Trim(),
            Status = User.IsInRole(PortalRoles.Admin) ? AppointmentStatus.Confirmed : AppointmentStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };

        database.Appointments.Add(appointment);
        try
        {
            await database.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            ModelState.AddModelError(nameof(model.StartsAt), "That appointment slot has just been reserved. Please pick another time.");
            await PopulateDepartmentsAndDoctors(model.DoctorId, model.DepartmentId);
            return View(model);
        }

        // Notify Doctor
        await notificationService.SendNotificationAsync(
            doctor.UserId,
            "New Appointment Request",
            $"{patientName} has requested a consultation for {model.StartsAt:ddd, MMM dd, yyyy h:mm tt}.",
            "Appointment",
            $"/Appointments/Details/{appointment.Id}");

        // Notify Patient
        await notificationService.SendNotificationAsync(
            targetPatientId,
            "Appointment Booking Submitted",
            $"Your appointment request with Dr. {doctor.User.DisplayName} on {model.StartsAt:ddd, MMM dd, yyyy h:mm tt} is submitted.",
            "Appointment",
            $"/Appointments/Details/{appointment.Id}");

        HttpContext.Session.SetInt32("LastAppointmentId", appointment.Id);
        TempData["Notice"] = User.IsInRole(PortalRoles.Admin)
            ? $"Appointment #{appointment.Id} booked and confirmed successfully for {patientName}."
            : "Your appointment request has been submitted successfully and is awaiting confirmation from the doctor.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var appointment = await database.Appointments
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Include(a => a.Doctor).ThenInclude(d => d.Department)
            .Include(a => a.Patient)
            .Include(a => a.MedicalRecord).ThenInclude(r => r!.Prescriptions)
            .Include(a => a.MedicalRecord).ThenInclude(r => r!.Documents)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (appointment is null) return NotFound();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!User.IsInRole(PortalRoles.Admin) &&
            appointment.PatientId != currentUserId &&
            appointment.Doctor.UserId != currentUserId)
        {
            return Forbid();
        }

        return View(appointment);
    }

    [HttpPost]
    [Authorize(Roles = $"{PortalRoles.Doctor},{PortalRoles.Admin}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(int id)
    {
        var appointment = await database.Appointments
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Include(a => a.Patient)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (appointment is null) return NotFound();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (User.IsInRole(PortalRoles.Doctor) && appointment.Doctor.UserId != currentUserId)
        {
            return Forbid();
        }

        if (appointment.Status != AppointmentStatus.Pending)
        {
            TempData["Error"] = "Only pending appointments can be confirmed.";
            return RedirectToAction(nameof(Index));
        }

        appointment.Status = AppointmentStatus.Confirmed;
        appointment.UpdatedAtUtc = DateTime.UtcNow;
        await database.SaveChangesAsync();

        await notificationService.SendNotificationAsync(
            appointment.PatientId,
            "Appointment Confirmed!",
            $"Dr. {appointment.Doctor.User.DisplayName} has confirmed your appointment for {appointment.StartsAt:ddd, MMM dd, yyyy h:mm tt} (Room: {appointment.Doctor.RoomNumber}).",
            "Appointment",
            $"/Appointments/Details/{appointment.Id}");

        TempData["Notice"] = $"Appointment #{appointment.Id} has been confirmed.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Roles = $"{PortalRoles.Doctor},{PortalRoles.Admin}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? reason)
    {
        var appointment = await database.Appointments
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Include(a => a.Patient)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (appointment is null) return NotFound();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (User.IsInRole(PortalRoles.Doctor) && appointment.Doctor.UserId != currentUserId)
        {
            return Forbid();
        }

        if (appointment.Status != AppointmentStatus.Pending)
        {
            TempData["Error"] = "Only pending appointments can be rejected.";
            return RedirectToAction(nameof(Index));
        }

        appointment.Status = AppointmentStatus.Rejected;
        appointment.RejectionReason = string.IsNullOrWhiteSpace(reason) ? "Doctor unavailable at requested time." : reason.Trim();
        appointment.UpdatedAtUtc = DateTime.UtcNow;
        await database.SaveChangesAsync();

        await notificationService.SendNotificationAsync(
            appointment.PatientId,
            "Appointment Request Declined",
            $"Your appointment request with Dr. {appointment.Doctor.User.DisplayName} for {appointment.StartsAt:g} was declined. Reason: {appointment.RejectionReason}",
            "Appointment",
            $"/Appointments/Details/{appointment.Id}");

        TempData["Notice"] = $"Appointment #{appointment.Id} was declined.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Roles = $"{PortalRoles.Patient},{PortalRoles.Admin}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var appointment = await database.Appointments
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Include(a => a.Patient)
            .SingleOrDefaultAsync(item => item.Id == id);

        if (appointment is null) return NotFound();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (User.IsInRole(PortalRoles.Patient) && appointment.PatientId != currentUserId)
        {
            return Forbid();
        }

        if (appointment.Status == AppointmentStatus.Completed || appointment.Status == AppointmentStatus.Cancelled)
        {
            TempData["Error"] = "This appointment cannot be cancelled.";
            return RedirectToAction(nameof(Index));
        }

        appointment.Status = AppointmentStatus.Cancelled;
        appointment.UpdatedAtUtc = DateTime.UtcNow;
        await database.SaveChangesAsync();

        await notificationService.SendNotificationAsync(
            appointment.Doctor.UserId,
            "Appointment Cancelled",
            $"Appointment #{appointment.Id} for {appointment.Patient.DisplayName} on {appointment.StartsAt:g} has been cancelled.",
            "Appointment",
            $"/Appointments/Details/{appointment.Id}");

        TempData["Notice"] = "Your appointment has been cancelled.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Roles = $"{PortalRoles.Doctor},{PortalRoles.Admin}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var appointment = await database.Appointments
            .Include(item => item.Doctor)
            .Include(item => item.Patient)
            .SingleOrDefaultAsync(item => item.Id == id && (User.IsInRole(PortalRoles.Admin) || item.Doctor.UserId == userId));

        if (appointment is null) return NotFound();

        if (appointment.Status != AppointmentStatus.Confirmed && appointment.Status != AppointmentStatus.Pending)
        {
            TempData["Error"] = "Only confirmed or pending appointments can be completed.";
            return RedirectToAction(nameof(Index));
        }

        appointment.Status = AppointmentStatus.Completed;
        appointment.UpdatedAtUtc = DateTime.UtcNow;
        await database.SaveChangesAsync();

        TempData["Notice"] = "Consultation marked as completed. Please record clinical notes and prescribe medications below.";
        return RedirectToAction("Create", "Records", new { appointmentId = appointment.Id });
    }

    // Dynamic slot availability API
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvailableSlots(int doctorId, string date)
    {
        if (doctorId <= 0 || !DateTime.TryParse(date, out var parsedDate))
        {
            return BadRequest(new { error = "Invalid doctor ID or date format." });
        }

        var doctor = await database.Doctors
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.Id == doctorId && d.IsActive);

        if (doctor is null)
        {
            return NotFound(new { error = "Doctor not found or inactive." });
        }

        var targetDate = parsedDate.Date;
        var dayOfWeek = targetDate.DayOfWeek.ToString();
        var allowedDays = doctor.AvailableDays.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        var isWorkingDay = allowedDays.Contains(dayOfWeek, StringComparer.OrdinalIgnoreCase);
        if (!isWorkingDay)
        {
            return Ok(new
            {
                isWorkingDay = false,
                message = $"Dr. {doctor.User.DisplayName} does not consult on {dayOfWeek}s.",
                slots = Array.Empty<AppointmentSlotDto>()
            });
        }

        // Fetch existing bookings for this doctor on that day
        var dayStart = targetDate;
        var dayEnd = targetDate.AddDays(1);
        var bookedAppointments = await database.Appointments
            .Where(a => a.DoctorId == doctorId &&
                        (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed) &&
                        a.StartsAt >= dayStart && a.StartsAt < dayEnd)
            .Select(a => a.StartsAt)
            .ToListAsync();

        var slots = new List<AppointmentSlotDto>();
        var currentSlotTime = doctor.StartTime;
        var now = DateTime.Now;

        while (currentSlotTime.AddMinutes(30) <= doctor.EndTime)
        {
            var slotDateTime = targetDate.Add(currentSlotTime.ToTimeSpan());
            var isPast = slotDateTime <= now;
            var isBooked = bookedAppointments.Any(b => b.Hour == slotDateTime.Hour && b.Minute == slotDateTime.Minute);

            slots.Add(new AppointmentSlotDto
            {
                TimeString = currentSlotTime.ToString("hh\\:mm tt"),
                DateTimeIso = slotDateTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                IsAvailable = !isPast && !isBooked,
                Reason = isPast ? "Slot has passed" : (isBooked ? "Booked" : "Available")
            });

            currentSlotTime = currentSlotTime.AddMinutes(30);
        }

        return Ok(new
        {
            isWorkingDay = true,
            doctorName = doctor.User.DisplayName,
            clinicHours = $"{doctor.StartTime:hh\\:mm tt} - {doctor.EndTime:hh\\:mm tt}",
            slots
        });
    }

    private async Task PopulateDepartmentsAndDoctors(int? selectedDoctorId = null, int? selectedDepartmentId = null)
    {
        var departments = await database.Departments
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .ToListAsync();

        ViewBag.Departments = new SelectList(departments, nameof(Department.Id), nameof(Department.Name), selectedDepartmentId);

        var doctorsQuery = database.Doctors
            .Include(d => d.User)
            .Where(d => d.IsActive)
            .OrderBy(d => d.User.DisplayName)
            .AsQueryable();

        if (selectedDepartmentId.HasValue && selectedDepartmentId.Value > 0)
        {
            doctorsQuery = doctorsQuery.Where(d => d.DepartmentId == selectedDepartmentId.Value);
        }

        var doctors = await doctorsQuery
            .Select(d => new
            {
                d.Id,
                DisplayName = $"{d.User.DisplayName} ({d.Specialty}) - ₹{d.ConsultationFee:0}"
            })
            .ToListAsync();

        ViewBag.Doctors = new SelectList(doctors, "Id", "DisplayName", selectedDoctorId);

        if (User.IsInRole(PortalRoles.Admin) || User.IsInRole(PortalRoles.Doctor))
        {
            var patients = await database.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.DisplayName)
                .Select(u => new
                {
                    u.Id,
                    DisplayName = $"{u.DisplayName} ({u.Email})"
                })
                .ToListAsync();

            ViewBag.Patients = new SelectList(patients, "Id", "DisplayName");
        }
    }
}
