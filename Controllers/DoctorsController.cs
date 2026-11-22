using System.Security.Claims;
using CsharpProject.Data;
using CsharpProject.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CsharpProject.Controllers;

[Authorize]
public sealed class DoctorsController(
    PortalDbContext database,
    UserManager<AppUser> userManager) : Controller
{
    // Public / Patient directory of doctors
    [AllowAnonymous]
    public async Task<IActionResult> Index(int? departmentId, string? search)
    {
        var query = database.Doctors
            .Include(d => d.User)
            .Include(d => d.Department)
            .Where(d => d.IsActive)
            .AsQueryable();

        if (departmentId.HasValue && departmentId.Value > 0)
        {
            query = query.Where(d => d.DepartmentId == departmentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(d =>
                d.User.DisplayName.ToLower().Contains(s) ||
                d.Specialty.ToLower().Contains(s) ||
                (d.Department != null && d.Department.Name.ToLower().Contains(s)));
        }

        ViewBag.Departments = await database.Departments
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .ToListAsync();
        ViewBag.SelectedDepartmentId = departmentId;
        ViewBag.SearchTerm = search;

        return View(await query.OrderBy(d => d.User.DisplayName).ToListAsync());
    }

    [AllowAnonymous]
    public async Task<IActionResult> Details(int id)
    {
        var doctor = await database.Doctors
            .Include(d => d.User)
            .Include(d => d.Department)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (doctor is null) return NotFound();

        return View(doctor);
    }

    // Admin doctor management
    [Authorize(Roles = PortalRoles.Admin)]
    public async Task<IActionResult> Manage()
    {
        var doctors = await database.Doctors
            .Include(d => d.User)
            .Include(d => d.Department)
            .OrderBy(d => d.User.DisplayName)
            .ToListAsync();

        return View(doctors);
    }

    [Authorize(Roles = PortalRoles.Admin)]
    public async Task<IActionResult> Create()
    {
        await PopulateDepartments();
        return View(new CreateDoctorViewModel());
    }

    [HttpPost]
    [Authorize(Roles = PortalRoles.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateDoctorViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.DisplayName))
        {
            ModelState.AddModelError(nameof(model.DisplayName), "Enter the clinician's name.");
        }

        if (string.IsNullOrWhiteSpace(model.Specialty))
        {
            ModelState.AddModelError(nameof(model.Specialty), "Enter the clinician's specialty.");
        }

        if (model.EndTime <= model.StartTime)
        {
            ModelState.AddModelError(nameof(model.EndTime), "Clinic closing time must be after opening time.");
        }

        var days = model.AvailableDays.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var validDays = Enum.GetNames<DayOfWeek>();
        if (days.Length == 0 || days.Any(day => !validDays.Contains(day, StringComparer.OrdinalIgnoreCase)))
        {
            ModelState.AddModelError(nameof(model.AvailableDays), "Enter weekday names separated by commas (e.g., Monday,Tuesday,Wednesday).");
        }

        if (await userManager.FindByEmailAsync(model.Email) is not null)
        {
            ModelState.AddModelError(nameof(model.Email), "An account with this email already exists.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateDepartments(model.DepartmentId);
            return View(model);
        }

        var doctorUser = new AppUser
        {
            UserName = model.Email,
            Email = model.Email,
            DisplayName = model.DisplayName.Trim(),
            EmailConfirmed = true,
            IsActive = true
        };

        var createResult = await userManager.CreateAsync(doctorUser, model.Password);
        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            await PopulateDepartments(model.DepartmentId);
            return View(model);
        }

        var roleResult = await userManager.AddToRoleAsync(doctorUser, PortalRoles.Doctor);
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(doctorUser);
            foreach (var error in roleResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            await PopulateDepartments(model.DepartmentId);
            return View(model);
        }

        var doctor = new Doctor
        {
            UserId = doctorUser.Id,
            DepartmentId = model.DepartmentId,
            Specialty = model.Specialty.Trim(),
            Qualification = model.Qualification.Trim(),
            ExperienceYears = model.ExperienceYears,
            ConsultationFee = model.ConsultationFee,
            RoomNumber = model.RoomNumber.Trim(),
            Bio = model.Bio?.Trim(),
            AvailableDays = string.Join(',', days.Select(day => Enum.Parse<DayOfWeek>(day, ignoreCase: true).ToString())),
            StartTime = model.StartTime,
            EndTime = model.EndTime,
            IsActive = true
        };

        database.Doctors.Add(doctor);
        await database.SaveChangesAsync();

        TempData["Notice"] = $"Doctor '{doctorUser.DisplayName}' added successfully.";
        return RedirectToAction(nameof(Manage));
    }

    [Authorize(Roles = PortalRoles.Admin)]
    public async Task<IActionResult> Edit(int id)
    {
        var doctor = await database.Doctors.Include(d => d.User).FirstOrDefaultAsync(d => d.Id == id);
        if (doctor is null) return NotFound();

        await PopulateDepartments(doctor.DepartmentId);
        var vm = new EditDoctorViewModel
        {
            Id = doctor.Id,
            DisplayName = doctor.User.DisplayName,
            DepartmentId = doctor.DepartmentId,
            Specialty = doctor.Specialty,
            Qualification = doctor.Qualification,
            ExperienceYears = doctor.ExperienceYears,
            ConsultationFee = doctor.ConsultationFee,
            RoomNumber = doctor.RoomNumber,
            Bio = doctor.Bio,
            AvailableDays = doctor.AvailableDays,
            StartTime = doctor.StartTime,
            EndTime = doctor.EndTime,
            IsActive = doctor.IsActive
        };

        return View(vm);
    }

    [HttpPost]
    [Authorize(Roles = PortalRoles.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditDoctorViewModel model)
    {
        if (id != model.Id) return BadRequest();

        var doctor = await database.Doctors.Include(d => d.User).FirstOrDefaultAsync(d => d.Id == id);
        if (doctor is null) return NotFound();

        if (model.EndTime <= model.StartTime)
        {
            ModelState.AddModelError(nameof(model.EndTime), "Clinic closing time must be after opening time.");
        }

        var days = model.AvailableDays.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var validDays = Enum.GetNames<DayOfWeek>();
        if (days.Length == 0 || days.Any(day => !validDays.Contains(day, StringComparer.OrdinalIgnoreCase)))
        {
            ModelState.AddModelError(nameof(model.AvailableDays), "Enter weekday names separated by commas.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateDepartments(model.DepartmentId);
            return View(model);
        }

        doctor.User.DisplayName = model.DisplayName.Trim();
        doctor.DepartmentId = model.DepartmentId;
        doctor.Specialty = model.Specialty.Trim();
        doctor.Qualification = model.Qualification.Trim();
        doctor.ExperienceYears = model.ExperienceYears;
        doctor.ConsultationFee = model.ConsultationFee;
        doctor.RoomNumber = model.RoomNumber.Trim();
        doctor.Bio = model.Bio?.Trim();
        doctor.AvailableDays = string.Join(',', days.Select(day => Enum.Parse<DayOfWeek>(day, ignoreCase: true).ToString()));
        doctor.StartTime = model.StartTime;
        doctor.EndTime = model.EndTime;
        doctor.IsActive = model.IsActive;

        await database.SaveChangesAsync();

        TempData["Notice"] = $"Doctor '{doctor.User.DisplayName}' details updated.";
        return RedirectToAction(nameof(Manage));
    }

    [HttpPost]
    [Authorize(Roles = PortalRoles.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var doctor = await database.Doctors.Include(d => d.User).FirstOrDefaultAsync(d => d.Id == id);
        if (doctor is null) return NotFound();

        doctor.IsActive = !doctor.IsActive;
        doctor.User.IsActive = doctor.IsActive;
        await database.SaveChangesAsync();

        TempData["Notice"] = $"Doctor status set to {(doctor.IsActive ? "Active" : "Inactive")}.";
        return RedirectToAction(nameof(Manage));
    }

    // Doctor personal schedule
    [Authorize(Roles = PortalRoles.Doctor)]
    public async Task<IActionResult> Schedule()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var doctor = await database.Doctors.Include(item => item.User).SingleOrDefaultAsync(item => item.UserId == userId);
        if (doctor is null) return NotFound();

        return View(new DoctorScheduleViewModel
        {
            AvailableDays = doctor.AvailableDays,
            StartTime = doctor.StartTime,
            EndTime = doctor.EndTime,
            Specialty = doctor.Specialty,
            ConsultationFee = doctor.ConsultationFee,
            RoomNumber = doctor.RoomNumber
        });
    }

    [HttpPost]
    [Authorize(Roles = PortalRoles.Doctor)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Schedule(DoctorScheduleViewModel model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var doctor = await database.Doctors.SingleOrDefaultAsync(item => item.UserId == userId);
        if (doctor is null) return NotFound();

        if (model.EndTime <= model.StartTime)
        {
            ModelState.AddModelError(nameof(model.EndTime), "Clinic closing time must be after opening time.");
        }

        var days = model.AvailableDays.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var validDays = Enum.GetNames<DayOfWeek>();
        if (days.Length == 0 || days.Any(day => !validDays.Contains(day, StringComparer.OrdinalIgnoreCase)))
        {
            ModelState.AddModelError(nameof(model.AvailableDays), "Enter weekday names separated by commas.");
        }

        if (!ModelState.IsValid)
        {
            model.Specialty = doctor.Specialty;
            return View(model);
        }

        doctor.AvailableDays = string.Join(',', days.Select(day => Enum.Parse<DayOfWeek>(day, ignoreCase: true).ToString()));
        doctor.StartTime = model.StartTime;
        doctor.EndTime = model.EndTime;
        doctor.ConsultationFee = model.ConsultationFee;
        doctor.RoomNumber = model.RoomNumber.Trim();

        await database.SaveChangesAsync();
        TempData["Notice"] = "Your clinic schedule and consultation details have been updated.";
        return RedirectToAction("Doctor", "Dashboard");
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetByDepartment(int departmentId)
    {
        var list = await database.Doctors
            .Include(d => d.User)
            .Where(d => d.IsActive && d.DepartmentId == departmentId)
            .OrderBy(d => d.User.DisplayName)
            .Select(d => new
            {
                d.Id,
                Name = d.User.DisplayName,
                d.Specialty,
                d.Qualification,
                d.ConsultationFee,
                d.RoomNumber,
                d.AvailableDays,
                StartTime = d.StartTime.ToString("hh\\:mm"),
                EndTime = d.EndTime.ToString("hh\\:mm")
            })
            .ToListAsync();

        return Json(list);
    }

    private async Task PopulateDepartments(int? selectedId = null)
    {
        var departments = await database.Departments
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .ToListAsync();

        ViewBag.Departments = new SelectList(departments, nameof(Department.Id), nameof(Department.Name), selectedId);
    }
}
