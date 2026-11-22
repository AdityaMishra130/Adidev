# adidev — Hospital Appointment Booking & Patient Record Portal
### Smart India Hackathon (SIH) MedTech / HealthTech Innovation Platform

**adidev** is a professional-grade, full-stack ASP.NET Core healthcare management platform designed for hospitals, clinical networks, and digital outpatient departments (OPDs). It delivers end-to-end appointment scheduling with real-time dynamic time slots, double-booking prevention, clinical consultation recording, vital signs tracking, digital multi-medicine prescriptions with printable PDF formats, electronic medical documents/reports repository, and in-app notifications with role-based access control (Admin, Doctor, Patient).

---

## 🌟 Key Features

### 1. Role-Based Portals & Dashboards
- **Admin Dashboard**: Real-time hospital metrics, capacity tracking, active doctor counts, department distributions, interactive Chart.js analytics for appointment statuses, doctor management, department management, and patient registry.
- **Doctor Dashboard**: OPD clinic management, today's consultation queue, pending patient appointment approval queue (1-click Accept / Decline with reason), clinical examination note recorder, vitals logger, digital prescription generator, and personal clinic hours configurator.
- **Patient Dashboard**: Personalized digital health summary (Blood Group, Allergies, Chronic Illnesses, Emergency Contacts), live appointment status tracking (Pending, Confirmed, Completed, Cancelled, Rejected), interactive 1-click booking, prescription archive, and diagnostic lab report repository.

### 2. Intelligent Appointment Booking & Scheduling
- **Cascading Selection**: Choose Department &rarr; Filter Doctors &rarr; Choose Date.
- **Dynamic 30-Minute Time Slots**: Live AJAX schedule engine fetches available, booked, and passed slots based on each doctor's specific operating days and hours.
- **Double-Booking Prevention**: Server-side and database-level filtered unique constraints (`[Status] <= 1`) ensure that no doctor can be double-booked for the same time slot.
- **Status Lifecycle**: Full lifecycle tracking (`Pending` &rarr; `Confirmed` &rarr; `Completed` or `Cancelled` / `Rejected`).

### 3. Comprehensive Medical Records & Printable Prescriptions
- **Clinical Consultations**: Records clinical diagnosis, examination observations, vitals (Blood Pressure, Heart Rate, Temperature, SpO2, Weight), and follow-up recommendation date.
- **Structured Multi-Medicine Prescriptions**: Medication name, dosage, frequency (1-0-1, etc.), duration, meal timing (Before/After food), and special instructions.
- **Professional Print View**: Dedicated, clean printable prescription format with hospital header, doctor license, patient vitals, classical ℞ table, advice notes, and digital signature block optimized for paper printing and PDF generation (`Ctrl+P` / `window.print()`).

### 4. Diagnostic Reports & Document Management
- **Secure File Storage**: Patient lab reports, blood work, radiology scans (X-Ray, MRI, CT Scan) with extension and size validation (max 10MB; PDF, JPG, PNG).
- **Access Control**: Strict data confidentiality ensuring patients only access their own records, doctors only access records for authorized consultations, and administrators oversee system health.

### 5. In-App Notification Center
- Real-time in-app alerts for appointment requests, approvals, cancellations, and prescription issuance.
- Live unread notification counter badge in the navigation header.

### 6. RESTful API & Dual Authentication
- **Web UI**: ASP.NET Core Identity with secure PBKDF2 password hashing, anti-forgery tokens, and HTTP-only cookies.
- **API Endpoints**: JWT Bearer token authentication on `/api/auth/token` and `/api/appointments` for interoperability with mobile apps and external healthcare systems.

---

## 🛠️ Technology Stack

- **Backend**: ASP.NET Core 10.0 MVC & Web API
- **Frontend**: Razor Views, HTML5, CSS3, JavaScript (ES6+), Bootstrap 5, Bootstrap Icons, Chart.js
- **Database**: Microsoft SQL Server / SQL Server LocalDB
- **ORM**: Entity Framework Core 10.0 (Code-First with Migrations)
- **Security & Identity**: ASP.NET Core Identity, JWT Bearer (`Microsoft.AspNetCore.Authentication.JwtBearer`), Role-Based Authorization (`Admin`, `Doctor`, `Patient`)
- **Session Management**: ASP.NET Core Distributed Memory Session

---

## 🚀 Quick Start / Run Locally

### Prerequisites
- [.NET 10 SDK or .NET 11 SDK](https://dotnet.microsoft.com/)
- SQL Server LocalDB (included with Visual Studio) or any SQL Server instance

### 1. Clone / Navigate to Project
```powershell
cd C:\Users\Aditya\OneDrive\Desktop\Csharp
```

### 2. Build the Project
```powershell
dotnet build
```

### 3. Run the Application
```powershell
dotnet run
```
The application will automatically:
1. Apply any pending Entity Framework Core migrations to your SQL Server LocalDB (`CarepointPortal`).
2. Create all identity roles (`Admin`, `Doctor`, `Patient`).
3. Seed default clinical departments (Cardiology, Neurology, Pediatrics, Orthopedics, General Medicine, Dermatology, Oncology).
4. Seed verified specialist doctors across departments with operating schedules and OPD rooms.
5. Seed default administrator and patient accounts.

Open your browser at:
```text
http://localhost:5172
```
*(or the port displayed in your terminal)*

---

## 🔑 Demo Login Accounts

| Role | Email | Password | Details |
| :--- | :--- | :--- | :--- |
| **Admin** | `admin@adidevhealth.com` | `Admin@12345` | Hospital Operations, Department & Staff Management, System Analytics |
| **Doctor** | `doctor.cardio@adidevhealth.com` | `Doctor@12345` | Dr. Rajesh Sharma (Interventional Cardiology) - OPD Queue & Prescriptions |
| **Doctor** | `doctor.pediatric@adidevhealth.com` | `Doctor@12345` | Dr. Priya Patel (Pediatrics & Neonatology) |
| **Doctor** | `doctor.neuro@adidevhealth.com` | `Doctor@12345` | Dr. Vikram Rao (Clinical Neurology) |
| **Patient** | `patient@adidevhealth.com` | `Patient@12345` | Aditya Kumar (Blood: O+, History: Mild Asthma) |

*You can also register a brand new patient account directly from the registration page.*

---

## 📡 REST API Usage

### 1. Generate JWT Token
```http
POST /api/auth/token
Content-Type: application/json

{
  "email": "patient@adidevhealth.com",
  "password": "Patient@12345"
}
```
**Response:**
```json
{
  "access_token": "eyJhbGciOi...",
  "token_type": "Bearer",
  "expires_in": 1800
}
```

### 2. Get Scoped Appointments
```http
GET /api/appointments
Authorization: Bearer <your_access_token>
```

### 3. Book Appointment via API
```http
POST /api/appointments
Authorization: Bearer <your_access_token>
Content-Type: application/json

{
  "doctorId": 1,
  "startsAt": "2026-11-25T10:00:00",
  "reason": "Chest pain checkup and ECG review"
}
```
*(Double booking the same doctor and slot automatically responds with `HTTP 409 Conflict`)*

---

## 📁 Project Architecture

```text
├── Controllers/
│   ├── AccountController.cs       # Authentication, patient registration, profile, patient directory
│   ├── ApiAppointmentsController.cs # Authenticated JWT RESTful API for appointments
│   ├── AppointmentsController.cs  # Appointment booking, dynamic slot generator, approval workflows
│   ├── DashboardController.cs     # Role-based dashboards (Admin, Doctor, Patient)
│   ├── DepartmentsController.cs   # Hospital departments & specialties management
│   ├── DoctorsController.cs       # Doctor directory, admin staff management, clinic hours
│   ├── DocumentsController.cs     # Secure medical reports & lab scans upload/download
│   ├── HomeController.cs          # Public landing page with metrics & department showcases
│   ├── NotificationsController.cs # In-app notification center & unread polling
│   ├── RecordsController.cs       # Medical records, vitals, prescriptions, print layout
│   └── TokenController.cs         # JWT token generator endpoint
├── Data/
│   ├── PortalDbContext.cs         # EF Core DbContext with filtered unique index & relations
│   └── PortalClaimsPrincipalFactory.cs # Custom claims enrichment
├── Migrations/                    # Entity Framework Core migrations
├── Models/                        # Domain entities & ViewModels
├── Services/                      # NotificationService & business abstractions
├── Views/                         # Responsive Razor views tailored per role
└── wwwroot/                       # Static assets (Bootstrap 5, CSS, JS, secure upload directory)
```
