# Changelog

A log of every task completed on HealthTrack, newest first.

## 2026-09-27 — Database context and Identity setup with roles

Commit: `290a598`

### Added
- `HealthTrack/Models/ApplicationUser.cs` — extends `IdentityUser` with `FullName` (required, max 100), `DateOfBirth` (optional `DateOnly`), `Gender` (optional, max 20) and `CreatedAt` (UTC, set on creation).
- `HealthTrack/Data/Roles.cs` — role name constants `Client`, `Trainer`, `Admin` and a `Roles.All` array.
- `HealthTrack/Data/ApplicationDbContext.cs` — `IdentityDbContext<ApplicationUser>` for MySQL via EF Core.
- `HealthTrack/Data/DbSeeder.cs` — runs on startup:
  - Applies pending migrations (`Database.MigrateAsync()`).
  - Creates the `Client`, `Trainer` and `Admin` roles if missing.
  - Creates an admin user from the `SeedAdmin:Email` / `SeedAdmin:Password` settings (skipped if not configured or the user already exists) and assigns the `Admin` role. Throws if user creation fails.
- `HealthTrack/Migrations/20260927042610_InitialIdentity` — initial migration creating the ASP.NET Core Identity tables: `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetRoleClaims`, `AspNetUserLogins`, `AspNetUserTokens`.
- `SeedAdmin` section in `HealthTrack/appsettings.Development.json` — development-only admin account `admin@healthtrack.local` / `Admin@12345`.

### Changed
- `HealthTrack/Program.cs`:
  - Reads the `DefaultConnection` connection string (throws if missing) and registers `ApplicationDbContext` with `UseMySQL`.
  - Registers Identity (`AddIdentity<ApplicationUser, IdentityRole>`) with EF stores and default token providers.
  - Identity options: unique email required; password minimum 8 characters, no special character required; lockout after 5 failed attempts for 15 minutes.
  - Auth cookie: HttpOnly, sliding expiration; paths `/Account/Login`, `/Account/Logout`, `/Account/AccessDenied` (these pages are not built yet).
  - Calls `DbSeeder.SeedAsync` at startup.
  - Adds `app.UseAuthentication()` before `app.UseAuthorization()`.
- `README.md` — ticked off "Database context and Identity setup with roles"; noted that migrations and seeding run on startup and documented the development admin login.

### Environment / verification
- Restored local tools (`dotnet tool restore`) and created the migration with `dotnet ef migrations add InitialIdentity`.
- Created the local MySQL database (`CREATE DATABASE IF NOT EXISTS healthtrack`) and applied the migration with `dotnet ef database update`.
- Build: succeeded with 0 warnings and 0 errors.
- Ran the app: home page returned HTTP 200 at `http://localhost:5182/`.
- Verified in MySQL: the three roles exist and `admin@healthtrack.local` is assigned the `Admin` role.

### Notes
- The dev admin password is committed in `appsettings.Development.json` for local use only; production should supply `SeedAdmin` via user secrets or environment variables.
- Login does not work yet because the `/Account/...` pages don't exist.

## 2026-09-27 — Project initialization

Commit: `44b8e0b`

### Added
- ASP.NET Core MVC project (`HealthTrack/`, .NET 10) with solution file `HealthTrack.slnx`.
- NuGet packages: `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.12, `Microsoft.EntityFrameworkCore.Design` 10.0.12, `Microsoft.EntityFrameworkCore.Tools` 10.0.12, `MySql.EntityFrameworkCore` 10.0.9.
- `dotnet-tools.json` pinning the local `dotnet-ef` CLI.
- Default MVC template: `HomeController`, `ErrorViewModel`, Home/Shared views, layout, `wwwroot` static files (Bootstrap, jQuery, jQuery Validation).
- `DefaultConnection` MySQL connection string in `appsettings.json`.
- `.gitignore` for .NET projects.
- `README.md` covering the project overview, guidelines (orange and white theme, keep code clean), system requirements, tech stack, setup steps, project structure and a progress checklist.
