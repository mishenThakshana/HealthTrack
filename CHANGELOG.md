# Changelog

A log of every task completed on HealthTrack, newest first.

## 2026-09-27 — Orange & white theme / layout

Commit: not committed yet

### Added
- `HealthTrack/Views/Shared/_LoginPartial.cshtml` — navbar section that depends on sign-in state:
  - Signed out: "Login" link and a white "Register" button (`/Account/Login`, `/Account/Register`).
  - Signed in: the user's name and a "Logout" button that posts to `/Account/Logout`.
  - The Account pages aren't built yet, so these links return 404 for now.
- Google Fonts **Inter** (weights 400–800) and **Bootstrap Icons 1.11.3** (jsDelivr CDN), loaded in the layout.

### Changed
- `HealthTrack/wwwroot/css/site.css` — rewritten as the theme:
  - Colour tokens on `:root`: `--ht-orange` `#ea580c`, `--ht-orange-dark` `#c2410c`, `--ht-orange-light` `#fff7ed`, `--ht-orange-soft` `#ffedd5`, text `#1f2937`, muted `#6b7280`, border `#f1e4d8`.
  - Bootstrap variables overridden: primary colour, link and hover colours, body text colour, font family (Inter), border radius `.5rem`.
  - `.btn-primary`, `.btn-outline-primary`, `.btn-light`, `.text-primary`, `.bg-primary-subtle`, form focus rings and checked checkboxes use the orange palette.
  - Sticky footer uses flexbox on `body` instead of the template's absolutely positioned footer.
  - New classes: `.ht-navbar` (orange bar with white links), `.ht-footer` (white with a 3px orange top border), `.ht-hero` (orange-to-white gradient banner), `.ht-card` (bordered card with an orange hover shadow), `.ht-icon` (orange rounded icon tile), `.ht-page-header` (heading with an orange underline).
  - Removed the template's blue colours (`#1b6ec2`, `#258cfb`, `#0077cc`).
- `HealthTrack/Views/Shared/_Layout.cshtml`:
  - Orange navbar with a heart-pulse brand icon, collapsing below the `md` breakpoint (`#mainNav`), plus `_LoginPartial` on the right.
  - Page content now sits directly in `<main>` so pages can use full-width sections; each page adds its own `.container`.
  - Footer shows the current year, a tagline and a Privacy link.
  - Removed the `HealthTrack.styles.css` link because there are no longer any per-view CSS files.
- `HealthTrack/Views/Home/Index.cshtml` — replaced the template welcome page with a hero section ("Track your health. Reach your goals.", "Get started" and "Sign in" buttons) and four feature cards: Workouts & Diet, Progress Tracking, Appointments, Reminders.
- `HealthTrack/Views/Home/Privacy.cshtml` — themed page header and a short privacy statement.
- `HealthTrack/Views/Shared/Error.cshtml` — themed "Something went wrong" page with the request ID and a "Back to home" button. Removed the developer-facing text about the Development environment.
- `README.md` — ticked off "Orange & white theme / layout".

### Removed
- `HealthTrack/Views/Shared/_Layout.cshtml.css` — its styles were merged into `site.css`.

### Environment / verification
- Build: succeeded with 0 warnings and 0 errors.
- Ran the app in Development on a separate port (`--urls http://localhost:5299`) because your `dotnet watch` session already held 5182. Home, Privacy and Error all returned HTTP 200.
- Checked headless Chrome screenshots at 1400px (desktop) and 520px (mobile, with the menu collapsed).
- Fixed a bug found in the screenshots: the collapsed-menu icon was rendering dark because of an unnecessary `filter: invert` rule, which I removed.
- Incident: during the first check, my cleanup command (`pkill -f "HealthTrack/bin"`) also stopped the app process running under your `dotnet watch`, which needs a restart or a file save to come back.

### Notes
- The fonts and icons load from CDNs, so the pages need internet access to look right.
- The pages are responsive. Screenshots narrower than about 500px aren't reliable in headless Chrome, which enforces a minimum window width.

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
