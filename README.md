# HealthTrack

**Personal Health & Fitness Management System** built with ASP.NET Core MVC and MySQL.

HealthTrack is a digital platform designed to help individuals track and improve their health and fitness. Users can monitor their exercise routines, diet, health metrics, and appointments with healthcare professionals. The system also provides tools for trainers and administrators to manage clients, track progress, and generate reports.

## Project Guidelines

- UI theme: **orange and white**.
- Keep code clean — no unnecessary long comments.

## System Requirements

### User Registration and Login
- Secure registration and login system for Users (clients), Trainers, and Administrators.
- Role-based access: Clients manage personal health data, Trainers manage clients' routines, Admins manage the system.

### Dashboard
- **Clients:** Overview of health stats, workout plans, diet schedules, and upcoming appointments.
- **Trainers:** Track client progress, assign routines, and provide feedback.
- **Administrators:** Overview of user activity, system usage, and trainer assignments.

### Workout & Diet Management
- Trainers can create personalized workout and diet plans for clients.
- Clients can log workouts, meals, and health measurements.

### Appointment System
- Clients can schedule consultations with trainers or healthcare professionals.
- Trainers can approve, update, or cancel appointments.

### Progress Tracking & Analytics
- Clients can see trends in weight, BMI, calories burned, or other health metrics.
- Trainers and Admins can generate reports on client performance, popular routines, and engagement statistics.

### Notifications
- Reminders for workouts, meals, or appointments.

### Security and Data Protection
- Protect sensitive health data using authentication, encryption, and input validation.
- Ensure compliance with basic data privacy standards.

## Tech Stack

| Layer | Technology |
|-------|------------|
| Framework | ASP.NET Core MVC (.NET 10) |
| Database | MySQL 8+ |
| ORM | Entity Framework Core 10 (`MySql.EntityFrameworkCore`) |
| Authentication | ASP.NET Core Identity |
| Frontend | Razor Views, Bootstrap, jQuery |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [MySQL Server 8+](https://dev.mysql.com/downloads/mysql/) (or MySQL via Docker / XAMPP / MAMP)
- An IDE: Visual Studio 2022+, JetBrains Rider, or VS Code with the C# Dev Kit

Verify the SDK:

```bash
dotnet --version
```

## Getting Started

### 1. Clone the repository

```bash
git clone <repository-url>
cd HealthTrack
```

### 2. Restore packages and local tools

```bash
dotnet restore
dotnet tool restore
```

`dotnet tool restore` installs the pinned `dotnet-ef` CLI from `dotnet-tools.json`.

### 3. Configure the database

Create the database in MySQL:

```sql
CREATE DATABASE healthtrack;
```

Update the connection string in `HealthTrack/appsettings.json` to match your MySQL credentials:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Port=3306;Database=healthtrack;User=root;Password=yourpassword;"
}
```

To avoid committing credentials, you can use user secrets instead:

```bash
cd HealthTrack
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3306;Database=healthtrack;User=root;Password=yourpassword;"
```

### 4. Apply database migrations

Once migrations exist in the project:

```bash
dotnet ef database update --project HealthTrack
```

To create a new migration after changing models:

```bash
dotnet ef migrations add <MigrationName> --project HealthTrack
```

### 5. Run the application

```bash
dotnet run --project HealthTrack
```

Or with hot reload:

```bash
dotnet watch --project HealthTrack
```

The app runs at:
- HTTP: http://localhost:5182
- HTTPS: https://localhost:7256 (use `--launch-profile https`)

If the HTTPS certificate is not trusted, run:

```bash
dotnet dev-certs https --trust
```

## Project Structure

```
HealthTrack/
├── HealthTrack.slnx          # Solution file
├── dotnet-tools.json         # Local tools (dotnet-ef)
├── system.txt                # Original system specification
└── HealthTrack/
    ├── Controllers/          # MVC controllers
    ├── Models/               # Domain and view models
    ├── Views/                # Razor views
    ├── wwwroot/              # Static files (css, js, lib)
    ├── Properties/           # Launch settings
    ├── appsettings.json      # App configuration & connection string
    └── Program.cs            # App startup and middleware
```

## Progress

- [x] Project initialization (ASP.NET Core MVC, EF Core, MySQL provider, Identity packages)
- [ ] Database context and Identity setup with roles (Client, Trainer, Admin)
- [ ] Orange & white theme / layout
- [ ] Registration and login
- [ ] Role-based dashboards
- [ ] Workout & diet management
- [ ] Appointment system
- [ ] Progress tracking & analytics
- [ ] Notifications
- [ ] Security hardening & data protection
