using System.Security.Claims;
using CsharpProject.Data;
using CsharpProject.Models;
using CsharpProject.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CsharpProject.Controllers;

[Authorize]
public sealed class DocumentsController(
    PortalDbContext database,
    IWebHostEnvironment environment,
    INotificationService notificationService) : Controller
{
    private static readonly string[] AllowedExtensions = [".pdf", ".jpg", ".jpeg", ".png"];
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    [HttpGet]
    public async Task<IActionResult> Index(string? patientId)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var targetPatientId = string.IsNullOrWhiteSpace(patientId) || User.IsInRole(PortalRoles.Patient)
            ? currentUserId
            : patientId;

        if (User.IsInRole(PortalRoles.Patient) && targetPatientId != currentUserId)
        {
            return Forbid();
        }

        var docs = await database.MedicalDocuments
            .Include(d => d.Patient)
            .Include(d => d.MedicalRecord)
            .Where(d => d.PatientId == targetPatientId)
            .OrderByDescending(d => d.UploadedAtUtc)
            .ToListAsync();

        ViewBag.TargetPatientId = targetPatientId;
        return View(docs);
    }

    [HttpGet]
    public IActionResult Upload(int? recordId, string? patientId)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var targetPatientId = User.IsInRole(PortalRoles.Patient) ? currentUserId! : (patientId ?? currentUserId!);

        return View(new MedicalDocumentUploadViewModel
        {
            MedicalRecordId = recordId,
            PatientId = targetPatientId
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(MedicalDocumentUploadViewModel model)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (User.IsInRole(PortalRoles.Patient) && model.PatientId != currentUserId)
        {
            return Forbid();
        }

        if (model.File is null || model.File.Length == 0)
        {
            ModelState.AddModelError(nameof(model.File), "Please select a valid file.");
        }
        else
        {
            var ext = Path.GetExtension(model.File.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
            {
                ModelState.AddModelError(nameof(model.File), "Only PDF, JPG, and PNG documents are supported.");
            }
            if (model.File.Length > MaxFileSizeBytes)
            {
                ModelState.AddModelError(nameof(model.File), "File size must not exceed 10 MB.");
            }
        }

        if (string.IsNullOrWhiteSpace(model.Title))
        {
            ModelState.AddModelError(nameof(model.Title), "Please provide a title for this document.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var uploadsFolder = Path.Combine(environment.WebRootPath, "uploads", "documents");
        Directory.CreateDirectory(uploadsFolder);

        var fileExt = Path.GetExtension(model.File!.FileName).ToLowerInvariant();
        var safeFileName = $"{Guid.NewGuid():N}{fileExt}";
        var fullPath = Path.Combine(uploadsFolder, safeFileName);

        await using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await model.File.CopyToAsync(stream);
        }

        var document = new MedicalDocument
        {
            PatientId = model.PatientId,
            MedicalRecordId = model.MedicalRecordId,
            Title = model.Title.Trim(),
            DocumentType = model.DocumentType,
            FileName = model.File.FileName,
            FilePath = $"/uploads/documents/{safeFileName}",
            FileSizeBytes = model.File.Length,
            ContentType = model.File.ContentType,
            UploadedAtUtc = DateTime.UtcNow
        };

        database.MedicalDocuments.Add(document);
        await database.SaveChangesAsync();

        await notificationService.SendNotificationAsync(
            model.PatientId,
            "Document Uploaded",
            $"Medical document '{document.Title}' ({document.DocumentType}) has been attached to your health records.",
            "MedicalRecord",
            $"/Documents/Index?patientId={model.PatientId}");

        TempData["Notice"] = "Document uploaded successfully.";
        return RedirectToAction(nameof(Index), new { patientId = model.PatientId });
    }

    [HttpGet]
    public async Task<IActionResult> Download(int id)
    {
        var doc = await database.MedicalDocuments.FindAsync(id);
        if (doc is null) return NotFound();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (User.IsInRole(PortalRoles.Patient) && doc.PatientId != currentUserId)
        {
            return Forbid();
        }

        var physicalPath = Path.Combine(environment.WebRootPath, doc.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (!System.IO.File.Exists(physicalPath))
        {
            return NotFound("File not found on storage server.");
        }

        return PhysicalFile(physicalPath, doc.ContentType, doc.FileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var doc = await database.MedicalDocuments.FindAsync(id);
        if (doc is null) return NotFound();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!User.IsInRole(PortalRoles.Admin) && doc.PatientId != currentUserId)
        {
            return Forbid();
        }

        var physicalPath = Path.Combine(environment.WebRootPath, doc.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (System.IO.File.Exists(physicalPath))
        {
            try { System.IO.File.Delete(physicalPath); } catch { /* log ignored */ }
        }

        database.MedicalDocuments.Remove(doc);
        await database.SaveChangesAsync();

        TempData["Notice"] = "Document removed.";
        return RedirectToAction(nameof(Index), new { patientId = doc.PatientId });
    }
}
