using CsharpProject.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CsharpProject.Data;

public sealed class PortalDbContext(DbContextOptions<PortalDbContext> options)
    : IdentityDbContext<AppUser>(options)
{
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<MedicalRecord> MedicalRecords => Set<MedicalRecord>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<MedicalDocument> MedicalDocuments => Set<MedicalDocument>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Doctor>()
            .HasOne(doctor => doctor.User)
            .WithOne()
            .HasForeignKey<Doctor>(doctor => doctor.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Doctor>()
            .HasOne(doctor => doctor.Department)
            .WithMany(dept => dept.Doctors)
            .HasForeignKey(doctor => doctor.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Appointment>()
            .HasOne(appointment => appointment.Patient)
            .WithMany()
            .HasForeignKey(appointment => appointment.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Appointment>()
            .HasOne(appointment => appointment.Doctor)
            .WithMany(doctor => doctor.Appointments)
            .HasForeignKey(appointment => appointment.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<MedicalRecord>()
            .HasOne(record => record.Patient)
            .WithMany()
            .HasForeignKey(record => record.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<MedicalRecord>()
            .HasOne(record => record.Doctor)
            .WithMany()
            .HasForeignKey(record => record.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<MedicalRecord>()
            .HasIndex(record => record.AppointmentId)
            .IsUnique();

        builder.Entity<MedicalDocument>()
            .HasOne(doc => doc.Patient)
            .WithMany()
            .HasForeignKey(doc => doc.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<MedicalDocument>()
            .HasOne(doc => doc.MedicalRecord)
            .WithMany(record => record.Documents)
            .HasForeignKey(doc => doc.MedicalRecordId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Notification>()
            .HasOne(notif => notif.User)
            .WithMany()
            .HasForeignKey(notif => notif.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Appointment>()
            .HasIndex(appointment => new { appointment.DoctorId, appointment.StartsAt })
            .IsUnique()
            .HasFilter("[Status] <= 1");
    }
}
