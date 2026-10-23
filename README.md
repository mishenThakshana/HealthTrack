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
- Clients can schedule consultations with trainers or healthcare professionals. (HealthTrack has three account types, as the brief lists: clients, trainers and administrators. Health-focused consultations are booked with trainers whose specialization covers them, for example Nutrition and diet or Injury recovery.)
- Trainers can approve, update, or cancel appointments.
- Administrators can manage every appointment.

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
| Email | MailKit over SMTP (Mailpit in development) |
| Charts | Chart.js 4.4.1 (served from `wwwroot/lib`) |
| Report exports | ClosedXML (Excel) and QuestPDF (PDF, Community licence) |
| Frontend | Razor Views, Bootstrap, jQuery |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker](https://www.docker.com/products/docker-desktop/) running SQL Server 2022 (or any SQL Server 2019+ instance) and [Mailpit](https://mailpit.axllent.org/) for test emails
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

### 4. Start Mailpit for emails

HealthTrack sends confirmation, password reset and notification emails over SMTP. In development they go to Mailpit, a local test inbox, so nothing reaches real addresses:

```bash
docker run -d --name mailpit --restart unless-stopped -p 1025:1025 -p 8025:8025 axllent/mailpit:v1.31.6
```

Open http://localhost:8025 to read the emails. An existing container can be started again with `docker start mailpit`. If Mailpit isn't running, the app still works; emails are retried and then dropped with an error in the log.

### 5. Apply database migrations

Migrations also run automatically on startup, which seeds the roles and, in Development, one account per role (see [Seeded accounts](#seeded-accounts)). To apply them manually:

```bash
dotnet ef database update --project HealthTrack
```

To create a new migration after changing models:

```bash
dotnet ef migrations add <MigrationName> --project HealthTrack
```

### 6. Run the application

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

In Development, this account is created on startup if it doesn't already exist. It is defined under `SeedUsers` in `HealthTrack/appsettings.Development.json`.

| Role | Email | Password | Name |
|------|-------|----------|------|
| Admin | `admin@healthtrack.local` | `Admin@12345` | System Administrator |

Clients and trainers sign up through the Register page. You can add more seed accounts to `SeedUsers`. Each entry takes `Email`, `Password`, `FullName` and `Role`, plus the optional details they'd enter at registration (for example `TrainerEmail`, `FitnessGoal`, `Specialization` and `Qualifications`).

These are for local development only. Don't use them in production.

## Registration and role details

- **Everyone:** full name, email, gender (Male or Female), password, and accepting the privacy policy. Admins can't register themselves and don't have a gender.
- **Clients** also give their date of birth. They don't fill in a role details section: their health data comes from what they log (measurements, workouts and meals). Older accounts may still hold a main goal, activity level and health conditions (encrypted); these show on the profile card but can no longer be edited.
- **Trainers** give their specialization, years of experience and one or more qualifications ("Add another qualification", up to 10; stored one per row in `TrainerQualifications`).
- The page only shows the section for the chosen account type. The other sections aren't validated or saved.
- The details are stored in `ClientProfiles` and `TrainerProfiles`. Each table has a one-to-one link to `AspNetUsers` (a unique `UserId`), and its row is deleted with the user. `TrainerQualifications` is one-to-many from `TrainerProfiles`.
- **Where they're used:**
  - Dashboards' profile cards.
  - The booking list ("Alex Morgan — Strength and muscle").
  - The appointment page.
  - The client's details for their trainer (Clients and Progress pages).
  - The admin's user page shows trainers' details; client health details stay private.
- **Editing and missing details:**
  - Trainers edit their details under **My account**.
  - New trainers, accounts created by an admin, and accounts whose role an admin changes are asked to add the details on their dashboard.
  - A role change removes the details kept for the old role.

## Profile pictures

- Everyone can upload, replace or remove a profile picture under **My account → Profile picture**. JPG, PNG or WebP, up to 2 MB.
- The file type is worked out from the file's first bytes, not from its name or the browser's Content-Type, so a renamed file is rejected. Uploads need the anti-forgery token, and requests over the size limit are refused.
- Pictures are stored in the `ProfilePhotos` table (one row per user, deleted with the user) and served by `/ProfilePhotos/Show/{id}?v=…`. `AspNetUsers.PhotoUpdatedAt` records when it last changed: it tells views whether to show the picture or the person's initials, and changes the URL so browsers fetch the new picture.
- **Who can see them:** trainers' pictures are visible to anyone signed in, so clients can browse them. A client's picture is only visible to the client, admins, their trainer, and trainers they have appointments with. Anyone else gets a 404.
- Pictures show in the navbar, the trainer dashboard profile card, Find a trainer and trainer profiles, the trainer's client list and client page, and the admin user list.
- `profile-pictures/` in the repository root has sample CC0 and public-domain pictures for each role (see its `CREDITS.md`).

## Find a trainer

- Clients open **Find a trainer** (`/Providers`) to browse trainers. Each card shows the picture, specialization, experience, qualifications, star rating and number of clients. Search by name, specialization or qualification, filter by specialization, and sort by name or top rated. Disabled accounts aren't listed.
- Until a client has chosen a trainer, a banner on every page (except Find a trainer itself) links to Find a trainer.
- Each trainer has a profile page (`/Providers/Details/{id}`) with their details, rating summary and reviews. Clients and admins can open it, and so can the trainer themselves (linked from their dashboard profile card as "Public profile"). Admins also get a "Profile & reviews" button on the user's page.
- A client can choose one trainer, switch to someone else (after confirming), or remove them. **Book** opens the Appointments page with that trainer preselected.
- Choosing a trainer sets the same `TrainerId` an admin sets on the Assignments page, so the trainer sees the client under Clients and can create plans for them. The admin Assignments page still works and shows these choices.
- The new trainer is notified, and so is the one being replaced or removed (`TrainerAssigned` and `ClientLeft` notifications, emailed if they have "Clients choosing or leaving me" switched on).

## Logging from your plans

- The client dashboard and the Workout log / Meal log pages show **Today's workout** and **Today's meals** from the client's active plans. Each item has a **Log** button that saves it to today's log in one click (exercise name, planned minutes and sets × reps; or meal type, description and calories), and a pencil that opens the log form pre-filled so it can be adjusted first. Exercises without planned minutes open the pre-filled form instead, because minutes are required.
- Logged items show a green **Logged** badge and can't be logged twice from the plan. Only items scheduled today in the client's own active plan can be logged this way.
- **Coming up** lists the next 6 days of the workout plan (exercises or "Rest day", and the plan's name when a different plan takes over) and says when a new workout or diet plan starts.
- A plan counts as active from its start date until its end date (if any); when plans overlap, the one that started last is used.

## Ratings and reviews

- After an appointment is marked as completed, or once an approved appointment has ended (even if the provider never marks it completed), the client can rate it from 1 to 5 stars and add optional feedback (up to 1,000 characters) on the appointment page. The completion notification and a "Rate your appointments" panel on the client dashboard point them there.
- One review per appointment (unique index). Clients can edit or delete their own reviews; admins can delete any review from the provider's profile page. A database check constraint keeps ratings between 1 and 5.
- Reviews are stored in `AppointmentReviews` (appointment, client, provider, rating, comment, dates). The comment is encrypted at rest like other free text, and reviews are deleted with their appointment, so deleting a client account removes them.
- The trainer is notified of each new review (`ReviewReceived`). They can read it on the appointment page.
- Ratings show as stars (average and count) on Find a trainer cards, trainer profiles and the trainer's own dashboard profile card. Profiles also show the 5-to-1 star breakdown and the latest 50 reviews. Other clients see reviewers by first name and initial ("Omar S."); admins see full names.

## Appointments

- Clients request a consultation from the Appointments page by choosing a trainer (their assigned trainer is preselected), date, time, length (30, 45, 60 or 90 minutes), type (in person, video call or phone call) and what they'd like to discuss.
- Appointments must start in the future, no more than 90 days ahead, and fit between 06:00 and 21:00.
- A request can't overlap the trainer's approved appointments or the client's own pending or approved ones.
- Trainers approve or decline requests, reschedule them (which also confirms them), add a location or meeting link and a note, cancel approved ones, and mark past ones as completed.
- Admins see every appointment on the Appointments page and can do everything a trainer can: approve, decline, reschedule, complete and cancel. Both the client and the trainer are notified, and the change is shown as made by "An administrator".
- Clients can cancel their own pending or approved appointments before they start. Pending requests that are never answered show as "Expired" once their start time passes.
- Times are stored in the server's local time.

## Progress tracking & analytics

- **Progress** (`/Progress`): clients see charts of weight, BMI, body fat, resting heart rate, workout minutes, calories burned and calories eaten (against their diet plan's target) for the last 30 days, 90 days, 6 months or 12 months, or a custom date range (any dates up to today, at most 2 years long), plus their top activities. Each chart has a "Show data" table. **Excel** and **PDF** buttons download the progress report for the chosen period (preset or custom): the summary plus every measurement (with BMI), workout and meal, top activities and trainer feedback. Trainers can export the report of their own clients from the same page.
- **Feedback**: a client's trainer can open the client's Progress page and leave feedback. Clients see it on their Progress page and dashboard. Authors can delete their own feedback.
- **Reports** (`/Reports`, trainers and admins): client performance (workouts, minutes, calories, weight change, active days, engagement level), engagement over time, appointments by status, popular plan exercises and most logged activities, for a preset period or a custom date range. Trainers see their own clients; admins see everyone. **Excel** and **PDF** buttons download the whole report (summary plus one sheet or section per table).
- **Admin dashboard**: active users this week, system usage over the last 14 days, totals, and each user's last sign-in.

## Emails and notifications

- **Account emails:** new accounts get a confirmation link. Until it's confirmed, a banner offers to resend it, and no notification emails are sent. Signing in doesn't need a confirmed email. "Forgot password?" on the sign-in page emails a reset link; it always shows the same message, so it doesn't reveal which emails have accounts. Changing your email asks you to confirm the new address and tells the old one. Password changes and resets send a security notice.
- **In-app notifications:** the bell in the top bar shows unread notifications. `/Notifications` lists them, with "Mark all as read" and "Clear read". Opening one marks it read and takes you to the related page.
- **What creates a notification:**
  - Appointments: requested, approved, declined, rescheduled, cancelled and completed (sent to the other person).
  - A new workout or diet plan, or a plan moved to you.
  - New progress feedback.
  - Being assigned a trainer, or a new client.
- **Reminders** (a background check every few minutes):
  - Appointments: 24 hours before an approved appointment, to the client and the provider.
  - Workouts: from 17:00 if your active workout plan has exercises today and you haven't logged a workout.
  - Meals: from 20:00 if you have an active diet plan and haven't logged a meal today.
  - Each reminder is sent once.
- **Preferences:** under My account → Notifications, choose which emails you get (appointments, reminders, updates) and turn workout or meal reminders off.
- **Configuration** (`appsettings.json`):
  - `Email`: SMTP host, port, SSL, optional username/password, from address, and `AppUrl` (used for links in emails sent by the background service).
  - `Reminders`: check interval, hours before an appointment, workout and meal times, and how many hours after those times reminders can still go out.
  - For a real SMTP provider, keep credentials in user secrets (`Email:UserName`, `Email:Password`).

## Security and data protection

- **Encryption at rest:**
  - What's encrypted: clients' injuries, health conditions and allergies, measurement and workout notes, meal descriptions, appointment reasons and notes, feedback and notification messages. They're encrypted in the database with ASP.NET Core Data Protection (AES-256 with HMAC) and stored with an `enc:` prefix. Older plain-text rows are encrypted automatically at startup.
  - Numbers such as weight stay unencrypted so charts and reports can be calculated. Use SQL Server TDE or disk encryption to cover everything.
- **Encryption keys:**
  - The keys live in `~/.healthtrack/keys` (override with `DataProtection:KeysPath`). **Back this folder up.** Without it, encrypted fields and existing sign-ins can't be read.
  - On a server, store the keys somewhere shared and protected, for example with `ProtectKeysWithCertificate`.
- **Sign-in:**
  - Passwords need 8+ characters with upper case, lower case, a digit and 4 different characters. Accounts lock for 15 minutes after 5 failed attempts.
  - Sign-in, registration, password and account-deletion forms are limited to 10 posts per minute per IP.
  - Sessions end after 8 hours without activity. Role changes, disabling and password resets take effect within a minute.
- **Cookies:** the `HealthTrack.Auth` and antiforgery cookies are HttpOnly and SameSite=Lax, and Secure outside Development.
- **Request protection:**
  - Every POST is checked for an antiforgery token (global filter).
  - Responses send a strict Content Security Policy (no inline scripts or styles), `X-Frame-Options: DENY`, `nosniff`, a Referrer-Policy, a Permissions-Policy and COOP. HSTS and HTTPS redirection are on in production.
  - Friendly pages for 400, 403, 404, 429 and 500.
- **Input validation:**
  - Server-side validation on every form (lengths, ranges, dates, enums, ownership checks).
  - Only local return URLs are followed.
  - Excel exports store every text value as text, so an entry like `=HYPERLINK(...)` is never run as a formula.
- **Privacy:**
  - New accounts accept the [privacy policy](HealthTrack/Views/Home/Privacy.cshtml), and the time is recorded.
  - Clients can export their progress report, with all their measurements, workouts and meals, as Excel or PDF (Progress page, linked from My account).
  - Clients can permanently delete their account and data, and admins can delete client accounts for erasure requests.
  - Read notifications are removed after 90 days and all notifications after a year.
  - Trainers' access to client data, and report and progress exports, are recorded in the application log. Logs don't include email addresses for failed sign-ins.
- **Secrets:** `appsettings.json` only has a placeholder connection string. Outside Development the app refuses to start until a real one is supplied.

## User management

Admins manage accounts at `/Users`: search and filter by role, create users with any role (including other admins), edit profiles, change roles, reset passwords, disable or re-enable accounts, and permanently delete client accounts. Admins can't change their own role or disable themselves. A trainer's role can't change while clients are assigned to them, and nobody's role can change while they have upcoming appointments. Role changes, password resets and disabling take effect within a minute.

## Who can do what

Everyone can edit their own profile, set a profile picture and change their password from **My account** in the account menu.

| Role | Pages | What they can do |
|------|-------|------------------|
| Admin | Dashboard, Users, Assignments, Reports | See user activity and system usage; create and manage accounts; assign each client to a trainer; run reports across all clients and export them as Excel or PDF |
| Trainer | Dashboard, Clients, Plans, Appointments, Reports | See assigned clients' measurements, logs and progress charts; give feedback; create, edit and delete workout and diet plans for them; approve, decline, reschedule, cancel and complete appointment requests; run reports on their clients and export them as Excel or PDF |
| Client | Dashboard, Find a trainer, Plans, Appointments, Track | Browse trainers, read their reviews and choose one; rate past appointments (completed, or approved and ended); view plans from their trainer; book consultations with a trainer and cancel them; log, edit and delete workouts, meals and measurements (weight, height, body fat, resting heart rate), or log planned workouts and meals in one click; see progress charts and feedback; export their progress report as Excel or PDF |

## Project Structure

```
HealthTrack/
├── HealthTrack.slnx          # Solution file
├── dotnet-tools.json         # Local tools (dotnet-ef)
└── HealthTrack/
    ├── Controllers/          # MVC controllers
    ├── Data/                 # DbContext, roles, startup seeding
    ├── Helpers/              # Date, period, BMI, account status and appointment rule helpers
    ├── Migrations/           # EF Core migrations (SQL Server)
    ├── Models/               # Domain and view models
    ├── Views/                # Razor views
    ├── wwwroot/              # Static files (css, js, lib incl. Chart.js)
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
- [x] Appointment system
- [x] Progress tracking & analytics
- [x] Admin user management, editable log entries
- [x] My account page (profile and password)
- [x] Emails (Mailpit) and notifications
- [x] Profile pictures; clients choose their own trainer
- [x] Security hardening & data protection
- [x] Role-specific registration and profiles
- [x] One-click logging of planned workouts and meals; upcoming workouts
- [x] Health professional role removed (not in the brief's account types); Excel and PDF report exports for trainers, admins and clients
