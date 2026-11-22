using System.Text;
using CsharpProject.Data;
using CsharpProject.Models;
using CsharpProject.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var jwtKey = builder.Configuration["Jwt:Key"] ?? "AdidevHealthcare_Secure_JWT_Secret_SIH2026_Minimum32BytesKey!";
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException("Configure Jwt:Key with at least 32 bytes using user-secrets or an environment variable.");
}

builder.Services.AddDbContext<PortalDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services
    .AddIdentity<AppUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<PortalDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddScoped<IUserClaimsPrincipalFactory<AppUser>, PortalClaimsPrincipalFactory>();
builder.Services.AddScoped<INotificationService, NotificationService>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

builder.Services.AddAuthentication()
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "AdidevHealth",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "AdidevHealthPortal",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = System.Security.Claims.ClaimTypes.Name,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Ensure upload folders exist
var uploadPath = Path.Combine(app.Environment.WebRootPath, "uploads", "documents");
Directory.CreateDirectory(uploadPath);

await using (var scope = app.Services.CreateAsyncScope())
{
    var services = scope.ServiceProvider;
    var database = services.GetRequiredService<PortalDbContext>();
    await database.Database.MigrateAsync();

    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in new[] { PortalRoles.Admin, PortalRoles.Doctor, PortalRoles.Patient })
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            var result = await roleManager.CreateAsync(new IdentityRole(role));
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Could not create role '{role}': {string.Join("; ", result.Errors.Select(error => error.Description))}");
            }
        }
    }

    var userManager = services.GetRequiredService<UserManager<AppUser>>();

    // Seed Departments
    if (!await database.Departments.AnyAsync())
    {
        var depts = new List<Department>
        {
            new() { Name = "Cardiology", Description = "Heart, vascular health, ECG, and cardiovascular interventions.", Icon = "bi-heart-pulse", IsActive = true },
            new() { Name = "Neurology", Description = "Brain, spine, neurological diagnosis and stroke management.", Icon = "bi-activity", IsActive = true },
            new() { Name = "Pediatrics", Description = "Comprehensive child and adolescent healthcare, immunizations.", Icon = "bi-emoji-smile", IsActive = true },
            new() { Name = "Orthopedics", Description = "Bones, joint replacements, sports injuries and trauma care.", Icon = "bi-person-arms-up", IsActive = true },
            new() { Name = "General Medicine", Description = "Primary care, adult health, chronic illness, and checkups.", Icon = "bi-capsule", IsActive = true },
            new() { Name = "Dermatology", Description = "Skin, hair, nail disorders, and allergy treatments.", Icon = "bi-shield-check", IsActive = true },
            new() { Name = "Oncology", Description = "Cancer care, medical oncology and preventive screening.", Icon = "bi-virus", IsActive = true }
        };
        database.Departments.AddRange(depts);
        await database.SaveChangesAsync();
    }

    // Seed Bootstrap Admin
    var adminEmail = app.Configuration["BootstrapAdmin:Email"] ?? "admin@adidevhealth.com";
    var adminPassword = app.Configuration["BootstrapAdmin:Password"] ?? "Admin@12345";
    var admin = await userManager.FindByEmailAsync(adminEmail);
    if (admin is null)
    {
        admin = new AppUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            DisplayName = "Adidev Administrator",
            PhoneNumber = "9876543200",
            EmailConfirmed = true,
            IsActive = true
        };
        var createResult = await userManager.CreateAsync(admin, adminPassword);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException($"Could not create bootstrap admin: {string.Join("; ", createResult.Errors.Select(error => error.Description))}");
        }
    }

    if (!await userManager.IsInRoleAsync(admin, PortalRoles.Admin))
    {
        await userManager.AddToRoleAsync(admin, PortalRoles.Admin);
    }

    // Seed Sample Clinicians if no doctors exist
    if (!await database.Doctors.AnyAsync())
    {
        var cardioDept = await database.Departments.FirstOrDefaultAsync(d => d.Name == "Cardiology");
        var pediaDept = await database.Departments.FirstOrDefaultAsync(d => d.Name == "Pediatrics");
        var neuroDept = await database.Departments.FirstOrDefaultAsync(d => d.Name == "Neurology");
        var genDept = await database.Departments.FirstOrDefaultAsync(d => d.Name == "General Medicine");

        var sampleDoctors = new[]
        {
            (Email: "doctor.cardio@adidevhealth.com", Name: "Dr. Rajesh Sharma", DeptId: cardioDept?.Id, Spec: "Interventional Cardiology", Qual: "MBBS, MD (Medicine), DM (Cardiology)", Exp: 14, Fee: 800m, Room: "OPD-102", Days: "Monday,Tuesday,Wednesday,Thursday,Friday"),
            (Email: "doctor.pediatric@adidevhealth.com", Name: "Dr. Priya Patel", DeptId: pediaDept?.Id, Spec: "Child Specialist & Neonatology", Qual: "MBBS, MD (Pediatrics), DNB", Exp: 9, Fee: 500m, Room: "OPD-204", Days: "Monday,Wednesday,Friday,Saturday"),
            (Email: "doctor.neuro@adidevhealth.com", Name: "Dr. Vikram Rao", DeptId: neuroDept?.Id, Spec: "Clinical Neurology & Neuro-rehab", Qual: "MBBS, MD, DM (Neurology)", Exp: 16, Fee: 1000m, Room: "OPD-305", Days: "Tuesday,Wednesday,Thursday,Friday"),
            (Email: "doctor.medicine@adidevhealth.com", Name: "Dr. Sneha Kulkarni", DeptId: genDept?.Id, Spec: "Internal Medicine & Diabetology", Qual: "MBBS, MD (General Medicine)", Exp: 11, Fee: 450m, Room: "OPD-105", Days: "Monday,Tuesday,Wednesday,Thursday,Friday,Saturday")
        };

        foreach (var doc in sampleDoctors)
        {
            var docUser = await userManager.FindByEmailAsync(doc.Email);
            if (docUser is null)
            {
                docUser = new AppUser
                {
                    UserName = doc.Email,
                    Email = doc.Email,
                    DisplayName = doc.Name,
                    PhoneNumber = "987654321" + doc.Room.Last(),
                    EmailConfirmed = true,
                    IsActive = true
                };
                var docCreate = await userManager.CreateAsync(docUser, "Doctor@12345");
                if (docCreate.Succeeded)
                {
                    await userManager.AddToRoleAsync(docUser, PortalRoles.Doctor);
                    database.Doctors.Add(new Doctor
                    {
                        UserId = docUser.Id,
                        DepartmentId = doc.DeptId,
                        Specialty = doc.Spec,
                        Qualification = doc.Qual,
                        ExperienceYears = doc.Exp,
                        ConsultationFee = doc.Fee,
                        RoomNumber = doc.Room,
                        AvailableDays = doc.Days,
                        StartTime = new TimeOnly(9, 0),
                        EndTime = new TimeOnly(17, 0),
                        IsActive = true,
                        Bio = $"{doc.Name} is a senior healthcare specialist with {doc.Exp} years of clinical excellence in {doc.Spec}."
                    });
                }
            }
        }
        await database.SaveChangesAsync();
    }

    // Seed Sample Patient
    var patientEmail = "patient@adidevhealth.com";
    var patientUser = await userManager.FindByEmailAsync(patientEmail);
    if (patientUser is null)
    {
        patientUser = new AppUser
        {
            UserName = patientEmail,
            Email = patientEmail,
            DisplayName = "Aditya Kumar",
            PhoneNumber = "9876501234",
            DateOfBirth = new DateOnly(2003, 5, 14),
            Gender = "Male",
            BloodGroup = "O+",
            Address = "Flat 402, Green Valley Apartments, New Delhi",
            EmergencyContactName = "Ramesh Kumar (Father)",
            EmergencyContactPhone = "9876509876",
            Allergies = "Penicillin, Sulfa drugs",
            ExistingConditions = "Mild seasonal asthma",
            EmailConfirmed = true,
            IsActive = true
        };
        var pResult = await userManager.CreateAsync(patientUser, "Patient@12345");
        if (pResult.Succeeded)
        {
            await userManager.AddToRoleAsync(patientUser, PortalRoles.Patient);
        }
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
