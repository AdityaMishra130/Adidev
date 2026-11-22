using System.Diagnostics;
using CsharpProject.Data;
using CsharpProject.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CsharpProject.Controllers;

public sealed class HomeController(PortalDbContext database) : Controller
{
    public async Task<IActionResult> Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        ViewBag.DoctorCount = await database.Doctors.CountAsync(d => d.IsActive);
        ViewBag.DepartmentCount = await database.Departments.CountAsync(d => d.IsActive);
        ViewBag.AppointmentCount = await database.Appointments.CountAsync(a => a.Status == AppointmentStatus.Completed);

        ViewBag.FeaturedDepartments = await database.Departments
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .Take(6)
            .ToListAsync();

        ViewBag.FeaturedDoctors = await database.Doctors
            .Include(d => d.User)
            .Include(d => d.Department)
            .Where(d => d.IsActive)
            .OrderBy(d => d.Id)
            .Take(4)
            .ToListAsync();

        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
