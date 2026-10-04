# HealthTrack

**Personal Health & Fitness Management System** built with ASP.NET Core MVC and Microsoft SQL Server.

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
| Database | Microsoft SQL Server 2022 (Docker) |
| ORM | Entity Framework Core 10 (`Microsoft.EntityFrameworkCore.SqlServer`) |
| Authentication | ASP.NET Core Identity |
| Frontend | Razor Views, Bootstrap, jQuery |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker](https://www.docker.com/products/docker-desktop/) running SQL Server 2022 (or any SQL Server 2019+ instance)
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

### 3. Start SQL Server and configure the connection

If you don't already have a SQL Server container, start one (the password must meet SQL Server's complexity rules):

```bash
docker run -d --name sql -p 1433:1433 \
  -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD='YourStrong@Passw0rd' \
  mcr.microsoft.com/mssql/server:2022-latest
```

An existing container can be started again with `docker start sql`.

`HealthTrack/appsettings.json` holds a template connection string with a `YOUR_PASSWORD` placeholder. Set the real one with user secrets so the password stays out of git (the project already has a `UserSecretsId`):

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Server=localhost,1433;Database=HealthTrack;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;MultipleActiveResultSets=True" \
  --project HealthTrack
```

User secrets are only loaded in the Development environment; elsewhere supply `ConnectionStrings__DefaultConnection` as an environment variable. `TrustServerCertificate=True` is needed because the Docker image uses a self-signed certificate.

You don't need to create the database — the first migration creates `HealthTrack` automatically.

### 4. Apply database migrations

Migrations also run automatically on startup, which seeds the roles and, in Development, one account per role (see [Seeded accounts](#seeded-accounts)). To apply them manually:

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

## Seeded accounts

In Development, these accounts are created on startup if they don't already exist. They are defined under `SeedUsers` in `HealthTrack/appsettings.Development.json`.

| Role | Email | Password | Name |
|------|-------|----------|------|
| Admin | `admin@healthtrack.local` | `Admin@12345` | System Administrator |
| Trainer | `trainer@healthtrack.local` | `Trainer@12345` | Alex Morgan |
| Client | `client@healthtrack.local` | `Client@12345` | Jamie Silva |

The seeded client is assigned to the seeded trainer, so you can sign in as the trainer and create plans for Jamie straight away.

These are for local development only. Don't use them in production.

## Who can do what

| Role | Pages | What they can do |
|------|-------|------------------|
| Admin | Dashboard, Assignments | See user statistics and assign each client to a trainer |
| Trainer | Dashboard, Clients, Plans | See assigned clients' measurements and logs; create, edit and delete workout and diet plans for them |
| Client | Dashboard, Plans, Track | View plans from their trainer; log workouts, meals and measurements (weight, height, body fat, resting heart rate) |

## Project Structure

```
HealthTrack/
├── HealthTrack.slnx          # Solution file
├── dotnet-tools.json         # Local tools (dotnet-ef)
└── HealthTrack/
    ├── Controllers/          # MVC controllers
    ├── Data/                 # DbContext, roles, startup seeding
    ├── Helpers/              # Date and BMI helpers
    ├── Migrations/           # EF Core migrations (SQL Server)
    ├── Models/               # Domain and view models
    ├── Views/                # Razor views
    ├── wwwroot/              # Static files (css, js, lib)
    ├── Properties/           # Launch settings
    ├── appsettings.json      # App configuration & connection string
    └── Program.cs            # App startup and middleware
```

## Progress

- [x] Project initialization (ASP.NET Core MVC, EF Core, Identity packages)
- [x] Database context and Identity setup with roles (Client, Trainer, Admin)
- [x] Orange & white theme / layout
- [x] Registration and login
- [x] Switched database from MySQL to SQL Server (Docker)
- [x] Role-based dashboards
- [x] Workout & diet management
- [ ] Appointment system
- [ ] Progress tracking & analytics
- [ ] Notifications
- [ ] Security hardening & data protection
