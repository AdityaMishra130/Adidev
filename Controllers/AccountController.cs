using System.Security.Claims;
using CsharpProject.Data;
using CsharpProject.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CsharpProject.Controllers;

public sealed class AccountController(
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager,
    PortalDbContext database) : Controller
{
    [AllowAnonymous]
    public IActionResult Register() => View();

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new AppUser
        {
            UserName = model.Email,
            Email = model.Email,
            DisplayName = model.DisplayName.Trim(),
            PhoneNumber = model.PhoneNumber?.Trim(),
            DateOfBirth = model.DateOfBirth,
            Gender = model.Gender,
            BloodGroup = model.BloodGroup,
            Address = model.Address?.Trim(),
            EmergencyContactName = model.EmergencyContactName?.Trim(),
            EmergencyContactPhone = model.EmergencyContactPhone?.Trim(),
            EmailConfirmed = true,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        var roleResult = await userManager.AddToRoleAsync(user, PortalRoles.Patient);
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            foreach (var error in roleResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        TempData["Notice"] = $"Welcome to Adidev Health Portal, {user.DisplayName}!";
        return RedirectToAction("Patient", "Dashboard");
    }

    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user is null || !user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Account not found or has been deactivated.");
            return View(model);
        }

        var result = await signInManager.PasswordSignInAsync(
            user, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl!);
            }

            if (await userManager.IsInRoleAsync(user, PortalRoles.Admin))
                return RedirectToAction("Admin", "Dashboard");
            if (await userManager.IsInRoleAsync(user, PortalRoles.Doctor))
                return RedirectToAction("Doctor", "Dashboard");
            if (await userManager.IsInRoleAsync(user, PortalRoles.Patient))
                return RedirectToAction("Patient", "Dashboard");

            return RedirectToAction("Index", "Home");
        }

        ModelState.AddModelError(string.Empty,
            result.IsLockedOut ? "This account is temporarily locked due to multiple failed attempts. Try again later." : "Invalid email or password.");
        return View(model);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    public async Task<IActionResult> Profile()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        var vm = new UserProfileViewModel
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber,
            DateOfBirth = user.DateOfBirth,
            Gender = user.Gender,
            BloodGroup = user.BloodGroup,
            Address = user.Address,
            EmergencyContactName = user.EmergencyContactName,
            EmergencyContactPhone = user.EmergencyContactPhone,
            Allergies = user.Allergies,
            ExistingConditions = user.ExistingConditions
        };

        return View(vm);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(UserProfileViewModel model)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        if (!ModelState.IsValid)
        {
            model.Email = user.Email ?? string.Empty;
            return View(model);
        }

        user.DisplayName = model.DisplayName.Trim();
        user.PhoneNumber = model.PhoneNumber?.Trim();
        user.DateOfBirth = model.DateOfBirth;
        user.Gender = model.Gender;
        user.BloodGroup = model.BloodGroup;
        user.Address = model.Address?.Trim();
        user.EmergencyContactName = model.EmergencyContactName?.Trim();
        user.EmergencyContactPhone = model.EmergencyContactPhone?.Trim();

        // Patients can update their own allergies and conditions
        if (User.IsInRole(PortalRoles.Patient))
        {
            user.Allergies = model.Allergies?.Trim();
            user.ExistingConditions = model.ExistingConditions?.Trim();
        }

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            foreach (var error in updateResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        TempData["Notice"] = "Profile details updated successfully.";
        return RedirectToAction(nameof(Profile));
    }

    [Authorize(Roles = $"{PortalRoles.Admin},{PortalRoles.Doctor}")]
    public async Task<IActionResult> Patients(string? search)
    {
        var patients = await userManager.GetUsersInRoleAsync(PortalRoles.Patient);
        var query = patients.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(p =>
                p.DisplayName.ToLower().Contains(s) ||
                (p.Email != null && p.Email.ToLower().Contains(s)) ||
                (p.PhoneNumber != null && p.PhoneNumber.Contains(s)) ||
                (p.BloodGroup != null && p.BloodGroup.ToLower().Contains(s)));
            ViewBag.SearchTerm = search;
        }

        return View(query.OrderBy(p => p.DisplayName).ToList());
    }

    [Authorize(Roles = $"{PortalRoles.Admin},{PortalRoles.Doctor}")]
    public async Task<IActionResult> PatientDetails(string id)
    {
        var patient = await userManager.FindByIdAsync(id);
        if (patient is null) return NotFound();

        ViewBag.Appointments = await database.Appointments
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Include(a => a.MedicalRecord)
            .Where(a => a.PatientId == id)
            .OrderByDescending(a => a.StartsAt)
            .ToListAsync();

        ViewBag.Records = await database.MedicalRecords
            .Include(r => r.Doctor).ThenInclude(d => d!.User)
            .Include(r => r.Prescriptions)
            .Where(r => r.PatientId == id || r.Appointment.PatientId == id)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync();

        ViewBag.Documents = await database.MedicalDocuments
            .Where(d => d.PatientId == id)
            .OrderByDescending(d => d.UploadedAtUtc)
            .ToListAsync();

        return View(patient);
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    [Authorize]
    public IActionResult ChangePassword() => View();

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(PasswordChangeViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var result = await userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        await signInManager.RefreshSignInAsync(user);
        TempData["Notice"] = "Your password has been updated.";
        return RedirectToAction("Index", "Dashboard");
    }
}
