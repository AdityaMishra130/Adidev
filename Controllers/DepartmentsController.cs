using CsharpProject.Data;
using CsharpProject.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CsharpProject.Controllers;

[Authorize]
public sealed class DepartmentsController(PortalDbContext database) : Controller
{
    [Authorize(Roles = PortalRoles.Admin)]
    public async Task<IActionResult> Index()
    {
        var departments = await database.Departments
            .Include(d => d.Doctors.Where(doc => doc.IsActive))
            .OrderBy(d => d.Name)
            .ToListAsync();

        return View(departments);
    }

    [Authorize(Roles = PortalRoles.Admin)]
    public IActionResult Create() => View(new Department());

    [HttpPost]
    [Authorize(Roles = PortalRoles.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Department model)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "Department name is required.");
        }

        if (await database.Departments.AnyAsync(d => d.Name == model.Name.Trim()))
        {
            ModelState.AddModelError(nameof(model.Name), "A department with this name already exists.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        model.Name = model.Name.Trim();
        model.Description = model.Description?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(model.Icon)) model.Icon = "bi-hospital";

        database.Departments.Add(model);
        await database.SaveChangesAsync();

        TempData["Notice"] = $"Department '{model.Name}' created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = PortalRoles.Admin)]
    public async Task<IActionResult> Edit(int id)
    {
        var department = await database.Departments.FindAsync(id);
        if (department is null) return NotFound();
        return View(department);
    }

    [HttpPost]
    [Authorize(Roles = PortalRoles.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Department model)
    {
        if (id != model.Id) return BadRequest();

        var department = await database.Departments.FindAsync(id);
        if (department is null) return NotFound();

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "Department name is required.");
        }

        if (await database.Departments.AnyAsync(d => d.Name == model.Name.Trim() && d.Id != id))
        {
            ModelState.AddModelError(nameof(model.Name), "Another department with this name already exists.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        department.Name = model.Name.Trim();
        department.Description = model.Description?.Trim() ?? string.Empty;
        department.Icon = string.IsNullOrWhiteSpace(model.Icon) ? "bi-hospital" : model.Icon.Trim();
        department.IsActive = model.IsActive;

        await database.SaveChangesAsync();

        TempData["Notice"] = $"Department '{department.Name}' updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Roles = PortalRoles.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var department = await database.Departments.FindAsync(id);
        if (department is null) return NotFound();

        department.IsActive = !department.IsActive;
        await database.SaveChangesAsync();

        TempData["Notice"] = $"Department '{department.Name}' {(department.IsActive ? "activated" : "deactivated")}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetList()
    {
        var list = await database.Departments
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .Select(d => new { d.Id, d.Name, d.Icon, d.Description })
            .ToListAsync();

        return Json(list);
    }
}
