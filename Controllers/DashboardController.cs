using System.Security.Claims;
using CsharpProject.Data;
using CsharpProject.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CsharpProject.Controllers;

[Authorize]
public sealed class DashboardController(
    PortalDbContext database,
    UserManager<AppUser> userManager) : Controller
{
    public IActionResult Index()
    {
        if (User.IsInRole(PortalRoles.Admin))
            return RedirectToAction(nameof(Admin));
        if (User.IsInRole(PortalRoles.Doctor))
            return RedirectToAction(nameof(Doctor));
        if (User.IsInRole(PortalRoles.Patient))
            return RedirectToAction(nameof(Patient));

        return RedirectToAction("Index", "Home");
    }

    [Authorize(Roles = PortalRoles.Admin)]
    public async Task<IActionResult> Admin()
    {
        var totalPatients = (await userManager.GetUsersInRoleAsync(PortalRoles.Patient)).Count;
        var totalDoctors = await database.Doctors.CountAsync(d => d.IsActive);
        var totalAppointments = await database.Appointments.CountAsync();
        var pendingAppointments = await database.Appointments.CountAsync(a => a.Status == AppointmentStatus.Pending);
        var confirmedAppointments = await database.Appointments.CountAsync(a => a.Status == AppointmentStatus.Confirmed);
        var completedAppointments = await database.Appointments.CountAsync(a => a.Status == AppointmentStatus.Completed);
        var cancelledAppointments = await database.Appointments.CountAsync(a => a.Status == AppointmentStatus.Cancelled);
        var totalDepartments = await database.Departments.CountAsync(d => d.IsActive);
        var totalMedicalRecords = await database.MedicalRecords.CountAsync();

        var departmentStats = await database.Departments
            .Where(d => d.IsActive)
            .Select(d => new DepartmentStatDto
            {
                DepartmentName = d.Name,
                DoctorCount = d.Doctors.Count(doc => doc.IsActive),
                AppointmentCount = d.Doctors.SelectMany(doc => doc.Appointments).Count()
            })
            .ToListAsync();

        var recentAppointments = await database.Appointments
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Include(a => a.Patient)
            .OrderByDescending(a => a.CreatedAtUtc)
            .Take(7)
            .ToListAsync();

        var activeDoctors = await database.Doctors
            .Include(d => d.User)
            .Include(d => d.Department)
            .Where(d => d.IsActive)
            .OrderByDescending(d => d.Id)
            .Take(5)
            .ToListAsync();

        var recentPatients = (await userManager.GetUsersInRoleAsync(PortalRoles.Patient))
            .OrderByDescending(p => p.CreatedAtUtc)
            .Take(5)
            .ToList();

        var model = new AdminDashboardViewModel
        {
            TotalPatients = totalPatients,
            TotalDoctors = totalDoctors,
            TotalAppointments = totalAppointments,
            PendingAppointments = pendingAppointments,
            ConfirmedAppointments = confirmedAppointments,
            CompletedAppointments = completedAppointments,
            CancelledAppointments = cancelledAppointments,
            TotalDepartments = totalDepartments,
            TotalMedicalRecords = totalMedicalRecords,
            DepartmentStats = departmentStats,
            RecentAppointments = recentAppointments,
            Doctors = activeDoctors,
            RecentPatients = recentPatients
        };

        return View(model);
    }

    [Authorize(Roles = PortalRoles.Doctor)]
    public async Task<IActionResult> Doctor()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var doctor = await database.Doctors
            .Include(d => d.User)
            .Include(d => d.Department)
            .FirstOrDefaultAsync(d => d.UserId == userId);

        if (doctor is null) return NotFound("Doctor profile not found.");

        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        var todayAppointments = await database.Appointments
            .Include(a => a.Patient)
            .Where(a => a.DoctorId == doctor.Id && a.StartsAt >= today && a.StartsAt < tomorrow)
            .OrderBy(a => a.StartsAt)
            .ToListAsync();

        var pendingRequests = await database.Appointments
            .Include(a => a.Patient)
            .Where(a => a.DoctorId == doctor.Id && a.Status == AppointmentStatus.Pending && a.StartsAt >= DateTime.Now)
            .OrderBy(a => a.StartsAt)
            .ToListAsync();

        var upcomingAppointments = await database.Appointments
            .Include(a => a.Patient)
            .Where(a => a.DoctorId == doctor.Id && a.Status == AppointmentStatus.Confirmed && a.StartsAt >= tomorrow)
            .OrderBy(a => a.StartsAt)
            .Take(10)
            .ToListAsync();

        var completedCount = await database.Appointments
            .CountAsync(a => a.DoctorId == doctor.Id && a.Status == AppointmentStatus.Completed);

        var totalPatients = await database.Appointments
            .Where(a => a.DoctorId == doctor.Id)
            .Select(a => a.PatientId)
            .Distinct()
            .CountAsync();

        var model = new DoctorDashboardViewModel
        {
            Doctor = doctor,
            TodayAppointments = todayAppointments,
            PendingRequests = pendingRequests,
            UpcomingAppointments = upcomingAppointments,
            CompletedCount = completedCount,
            TotalPatientCount = totalPatients
        };

        return View(model);
    }

    [Authorize(Roles = PortalRoles.Patient)]
    public async Task<IActionResult> Patient()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var patient = await userManager.FindByIdAsync(userId!);
        if (patient is null) return NotFound("Patient account not found.");

        var upcomingAppointments = await database.Appointments
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Include(a => a.Doctor).ThenInclude(d => d.Department)
            .Where(a => a.PatientId == userId && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed) && a.StartsAt >= DateTime.Now)
            .OrderBy(a => a.StartsAt)
            .ToListAsync();

        var pastAppointments = await database.Appointments
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Include(a => a.MedicalRecord)
            .Where(a => a.PatientId == userId && (a.Status == AppointmentStatus.Completed || a.StartsAt < DateTime.Now))
            .OrderByDescending(a => a.StartsAt)
            .Take(5)
            .ToListAsync();

        var recentRecords = await database.MedicalRecords
            .Include(r => r.Appointment).ThenInclude(a => a.Doctor).ThenInclude(d => d.User)
            .Include(r => r.Prescriptions)
            .Where(r => r.PatientId == userId || r.Appointment.PatientId == userId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(5)
            .ToListAsync();

        var documents = await database.MedicalDocuments
            .Where(d => d.PatientId == userId)
            .OrderByDescending(d => d.UploadedAtUtc)
            .Take(5)
            .ToListAsync();

        var model = new PatientDashboardViewModel
        {
            Patient = patient,
            UpcomingAppointments = upcomingAppointments,
            PastAppointments = pastAppointments,
            RecentRecords = recentRecords,
            Documents = documents
        };

        return View(model);
    }
}
