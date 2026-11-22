using System.Security.Claims;
using CsharpProject.Data;
using CsharpProject.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CsharpProject.Controllers;

[ApiController]
[Route("api/appointments")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class ApiAppointmentsController(PortalDbContext database) : ControllerBase
{
    public sealed record BookAppointmentDto(int DoctorId, DateTime StartsAt, string Reason);

    [HttpGet]
    public async Task<IActionResult> GetAppointments()
    {
        var query = database.Appointments.AsNoTracking()
            .Include(appointment => appointment.Doctor).ThenInclude(doctor => doctor.User)
            .Include(appointment => appointment.Patient)
            .AsQueryable();

        if (User.IsInRole(PortalRoles.Patient))
        {
            var patientId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            query = query.Where(appointment => appointment.PatientId == patientId);
        }
        else if (User.IsInRole(PortalRoles.Doctor))
        {
            var doctorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            query = query.Where(appointment => appointment.Doctor.UserId == doctorUserId);
        }
        else if (!User.IsInRole(PortalRoles.Admin))
        {
            return Forbid();
        }

        var appointments = await query.OrderBy(appointment => appointment.StartsAt)
            .Select(appointment => new
            {
                appointment.Id,
                appointment.StartsAt,
                Status = appointment.Status.ToString(),
                appointment.Reason,
                Patient = User.IsInRole(PortalRoles.Patient) ? null : appointment.Patient.DisplayName,
                Doctor = appointment.Doctor.User.DisplayName,
                Specialty = appointment.Doctor.Specialty,
                Room = appointment.Doctor.RoomNumber
            })
            .ToListAsync();

        return Ok(appointments);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetAppointment(int id)
    {
        var apt = await database.Appointments.AsNoTracking()
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Include(a => a.Patient)
            .Include(a => a.MedicalRecord).ThenInclude(r => r!.Prescriptions)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (apt is null) return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!User.IsInRole(PortalRoles.Admin) && apt.PatientId != userId && apt.Doctor.UserId != userId)
        {
            return Forbid();
        }

        return Ok(new
        {
            apt.Id,
            apt.StartsAt,
            Status = apt.Status.ToString(),
            apt.Reason,
            Patient = apt.Patient.DisplayName,
            Doctor = apt.Doctor.User.DisplayName,
            Specialty = apt.Doctor.Specialty,
            Room = apt.Doctor.RoomNumber,
            MedicalRecord = apt.MedicalRecord == null ? null : new
            {
                apt.MedicalRecord.Id,
                apt.MedicalRecord.Diagnosis,
                apt.MedicalRecord.Notes,
                apt.MedicalRecord.BloodPressure,
                apt.MedicalRecord.HeartRate,
                apt.MedicalRecord.Temperature,
                Prescriptions = apt.MedicalRecord.Prescriptions.Select(p => new
                {
                    p.Medication,
                    p.Dosage,
                    p.Frequency,
                    p.Duration,
                    p.MealTiming
                })
            }
        });
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> BookAppointment([FromBody] BookAppointmentDto dto)
    {
        var doctor = await database.Doctors.FindAsync(dto.DoctorId);
        if (doctor is null || !doctor.IsActive)
        {
            return BadRequest(new { error = "Invalid or inactive doctor." });
        }

        if (dto.StartsAt <= DateTime.Now)
        {
            return BadRequest(new { error = "Appointment time must be in the future." });
        }

        var isBooked = await database.Appointments.AnyAsync(a =>
            a.DoctorId == dto.DoctorId &&
            (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed) &&
            a.StartsAt == dto.StartsAt);

        if (isBooked)
        {
            return Conflict(new { error = "Slot is already booked." });
        }

        var patientId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var appointment = new Appointment
        {
            DoctorId = dto.DoctorId,
            PatientId = patientId,
            StartsAt = dto.StartsAt,
            Reason = dto.Reason,
            Status = AppointmentStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };

        database.Appointments.Add(appointment);
        await database.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAppointment), new { id = appointment.Id }, new
        {
            appointment.Id,
            appointment.StartsAt,
            Status = appointment.Status.ToString(),
            message = "Appointment booked successfully."
        });
    }
}
