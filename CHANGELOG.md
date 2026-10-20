# Changelog

A log of every task completed on HealthTrack, newest first.

## 2026-10-20 — Security hardening & data protection

Commit: not committed yet

### Added
- **Field-level encryption** (`Data/ProtectedStringConverter.cs`):
  - An EF value converter using ASP.NET Core Data Protection (purpose `HealthTrack.HealthData.v1`, AES-256-CBC + HMACSHA256). Stored values get an `enc:` prefix; values without it are read as plain text.
  - Applied through `ApplicationDbContext.EncryptedColumns`: `HealthMeasurements.Notes`, `WorkoutLogs.Notes`, `MealLogs.Description`, `Appointments.Reason`, `Appointments.TrainerNote`, `ProgressFeedback.Message`, `Notifications.Message`. These columns are now `nvarchar(max)`; length limits stay in the form view models.
  - `DbSeeder.EncryptLegacyRowsAsync` finds rows without the prefix at startup (raw SQL built only from the fixed column list) and re-saves them encrypted.
  - Data Protection keys are stored in `~/.healthtrack/keys` (configurable as `DataProtection:KeysPath`), with application name `HealthTrack`.
- **Security headers** (`Helpers/SecurityHeaders.cs`):
  - Content-Security-Policy: `default-src 'self'`, scripts from self only, styles and fonts from self plus Google Fonts and jsDelivr, `img-src 'self' data:`, `object-src 'none'`, `base-uri 'self'`, `form-action 'self'`, `frame-ancestors 'none'`. Development also allows `ws:`/`wss:` for `dotnet watch`.
  - Also `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, a Permissions-Policy (camera, microphone, geolocation and payment off) and `Cross-Origin-Opener-Policy: same-origin`.
- **Rate limiting** (`RateLimits.Auth`): fixed window, 10 per minute per IP, on POST Login, Register, UpdateProfile, ChangePassword, ResendConfirmation, ForgotPassword, ResetPassword and DeleteAccount. Rejected requests get 429.
- **Status pages:** `UseStatusCodePagesWithReExecute("/Home/Status/{0}")` and a `Home/Status` view for 404, 403, 429, 400 ("the form may have expired") and anything else. It ignores antiforgery, so a POST that failed the token check still gets the page.
- **Privacy and data rights:**
  - `ApplicationUser.PrivacyAcceptedAt`. Registration requires ticking "I have read and accept the privacy policy" (linked).
  - **Download my data** (`Account/DownloadData`): a JSON file with profile, settings, measurements, workouts, meals, workout and diet plans (as client or author), appointments (as client or provider), feedback and notifications, with encrypted fields decrypted.
  - **Delete my account** (clients, `Account/DeleteAccount`): needs the current password and an "I understand" box, and is rate-limited. `Services/AccountDeletion` removes, in one transaction, the user's notifications plus notifications linking to their appointments, feedback, appointments, plans with their exercises and meals, logs, measurements, and finally the Identity user. The user is signed out and gets a confirmation email if their address was confirmed.
  - Admins can **delete client accounts** (`Users/Delete`), for example after an erasure request. Other roles are refused ("Disable other accounts instead"), and you can't delete yourself.
  - **Notification retention:** `ReminderService` removes read notifications older than 90 days and all notifications older than 365 days (`Reminders:ReadNotificationRetentionDays`, `NotificationRetentionDays`).
  - **Privacy page** rewritten: what's stored, why, who can see it per role, protections, retention, rights (access, correction, erasure, choice) and cookies.
- **Access logging:** a trainer viewing client details, a provider viewing a client's progress, report CSV exports, data downloads and account deletions are written to the application log.

### Changed
- `Program.cs`:
  - `RequiredUniqueChars = 4`.
  - Auth cookie renamed `HealthTrack.Auth`, SameSite=Lax, Secure outside Development, 8-hour sliding expiry. Antiforgery cookie renamed and Secure outside Development.
  - A global `AutoValidateAntiforgeryTokenAttribute`.
  - Startup fails outside Development if the connection string still holds `YOUR_PASSWORD`.
  - The new middleware order: security headers, status pages, HTTPS, routing, rate limiter, auth.
- Inline styles and scripts removed so the CSP can block them:
  - The empty `importmap` script in the layout.
  - The hero icon size (now `.ht-hero-icon`).
  - The meter bar widths (now `data-width`, set by `site.js`).
- `site.js`: the boilerplate comments are gone.
- Failed sign-in lockouts log the user id instead of the email address.
- `appsettings.json`: `DataProtection:KeysPath` and the retention settings.
- My account: a **Your data** panel (download, plus delete for clients; other roles are told to ask an admin). Users → Edit: "Delete account and data" for clients.
- README: a "Security and data protection" section, key backup advice, and progress ticked.
- Migration `SecurityAndPrivacy`: encrypted columns become `nvarchar(max)`, and `AspNetUsers.PrivacyAcceptedAt` is added.

### Environment / verification
- Build: succeeded with 0 warnings and 0 errors. `dotnet list package --vulnerable --include-transitive`: none.
- Ran on port 5299:
  - **Legacy encryption:** inserted two plain-text rows with sqlcmd and restarted. Both were stored as `enc:CfDJ8…` and still showed decrypted to the client and the trainer.
  - New measurement, workout, meal, appointment reason and notification message all stored encrypted. The data export showed them decrypted.
  - Old auth cookies stopped working after the key change, as expected (sign in again once).
  - **Headers:** CSP, nosniff, DENY, Referrer-Policy, Permissions-Policy and COOP present. The auth cookie is HttpOnly + SameSite=Lax.
  - **Status pages:** an unknown URL and another user's appointment → "Page not found". A POST without a token → 400 "Something didn't look right".
  - **Rate limiting:** after 10 sign-in POSTs in a minute → 429 "Too many attempts". GET pages still work.
  - **CSP in a real browser** (headless Chrome, console and exceptions captured) on Progress, client dashboard, My account, Reports, admin dashboard, Users, Notifications, Register, Privacy and Home: no console errors. Charts, dropdowns and meter bars still render.
  - **Registration:** without consent → error. A password like `Aa1Aa1Aa1` → "at least 4 different characters". A valid registration records `PrivacyAcceptedAt`.
  - **Self-deletion:** a wrong password or unticked box → nothing deleted. A trainer posting to it → Access Denied. A valid deletion → user, logs, meals, measurements, appointment and the provider's request notification removed, signed out, "account deleted" email sent, and the old password no longer signs in.
  - **Admin deletion:** a client deleted. A trainer → refused, and there's no delete button for non-clients.
  - **Retention:** of four inserted notifications (read 100 days ago, unread 100 days, unread 400 days, read 10 days ago), the restart removed the first and third ("Removed 2 old notifications").
  - **Access log lines** for client details, progress views and CSV export appeared.
- **Cleanup:**
  - Deleted all test users (`d1`, `d2`), logs, measurements, appointments and notifications, and reset identity seeds.
  - Emptied Mailpit and stopped only the port-5299 process.
  - Your workout plan and accounts are untouched.

### Notes
- **The encryption keys matter.** Back up `~/.healthtrack/keys`; without it the encrypted fields can't be read. On macOS and Linux the key files aren't themselves encrypted, so for production protect them (certificate, Azure Key Vault and so on) and use a shared location if there's more than one server.
- Numbers (weight, calories, dates) aren't field-encrypted because charts and reports query them. SQL Server Transparent Data Encryption or disk encryption covers the whole database.
- Only client accounts can be deleted, because plans, appointments and feedback written by staff belong to clients' records. Staff accounts are disabled instead.
- The `SecurityAndPrivacy` Down migration shrinks the encrypted columns back, which would fail once encrypted data is stored.
- The real SQL password is still in commit `ba40a85` on GitHub (see the previous entry).

## 2026-10-20 — Emails (Mailpit) and notifications

Commit: not committed yet

### Added
- **Mailpit** test inbox, running in Docker as `mailpit`. SMTP is on `localhost:1025` and the web inbox is http://localhost:8025. The container restarts with Docker.
- **Email sending** (`Services/`):
  - `EmailOptions`: the `Email` config section (Enabled, Host, Port, UseSsl, UserName, Password, FromAddress, FromName, AppUrl).
  - `EmailQueue`: an unbounded channel.
  - `EmailSenderService`: a hosted service that sends queued messages with MailKit, so requests never wait on SMTP. It makes up to 3 attempts with 5s/10s back-off, then logs an error. When `Enabled` is false it only logs.
  - `EmailTemplates.Render`: an orange-branded HTML email with a plain-text version (heading, greeting, paragraphs, optional button plus a copyable link, footer). All content is HTML-encoded.
  - Packages: `MailKit` 4.18.1, and `System.Security.Cryptography.Pkcs` 10.0.13 pinned to fix the moderate/high advisories in the 10.0.0 version MimeKit pulls in.
- **Account emails** (`AccountEmails`, `AccountController`):
  - **Confirmation:** a confirmation email on registration. `ConfirmEmail` (Base64Url-encoded token) shows "Email confirmed" or "Confirmation failed". `ResendConfirmation` (POST). A site-wide banner for signed-in users whose email isn't confirmed, with a "Resend link" button. Confirmation is not required to sign in, so existing accounts (such as `johndoe@gmail.com`) aren't locked out; unconfirmed addresses just don't get notification emails.
  - **Forgot password / reset password:** `ForgotPassword` always shows the same "If an account exists…" message (no account enumeration) and doesn't email disabled accounts. `ResetPassword` takes the emailed link. Invalid or reused tokens get "invalid or has expired". A successful reset clears failed sign-in attempts and redirects to sign-in. There's a "Forgot password?" link on the sign-in page.
  - **Email change** (My account): the new address becomes unconfirmed and gets a confirmation link, and the old address gets a "your email was changed" notice.
  - **Security notices:** "Your password was changed" after a password change, a reset, or an admin reset, sent only to confirmed addresses.
- **Notifications:**
  - `Notification` entity: user, `Type` (`NotificationType` enum stored as a string), title (150), message (500), link (300), `Key` (100, for de-duplicating reminders), `CreatedAt`, `ReadAt`. Indexes on `(UserId, ReadAt, CreatedAt)` and a filtered unique index on `(UserId, Key)`.
  - `NotificationService.NotifyAsync`: skips existing keys, saves the in-app notification and queues an email when the user's email is confirmed and their preference for that category allows it. A unique-key race is caught. Categories: Appointments, Reminders, Updates. Each type has an icon.
  - Event notifications:
    - Appointments: requested (to the provider), approved, declined (with reason), rescheduled/updated (with location), completed (to the client), and cancelled (to the other party, with the provider's reason).
    - A new workout or diet plan, or a plan moved to another client (to the client).
    - Progress feedback (to the client).
    - Trainer assignment (to the client and the trainer).
  - `ReminderService` (hosted, `Reminders` config section: CheckIntervalMinutes 5, AppointmentHoursBefore 24, WorkoutTime 17:00, MealTime 20:00, WindowHours 4):
    - Appointment reminders to both people for approved appointments starting within 24 hours. The key includes the start time, so a rescheduled appointment gets a fresh reminder.
    - Workout reminders when the active plan has exercises for today (or any day) and nothing is logged.
    - Meal reminders when there's an active diet plan and no meals are logged today.
    - Reminders only go out inside the window after the configured time, and each key is used once a day.
  - **Bell** in the top bar (`NotificationBellViewComponent`): unread count (shows 9+ above nine) and a dropdown of the latest 5 with icon, title, two-line message, time ago, and "See all".
  - `NotificationsController` (`/Notifications`): the latest 50, unread highlighted with a "New" badge. `Open/{id}` marks it read and redirects to the link (local URLs only; other users' notifications → 404). "Mark all as read" (`ExecuteUpdate`), "Clear read" (`ExecuteDelete`), and a Settings link.
  - **Preferences** on `ApplicationUser`: `EmailAppointments`, `EmailReminders`, `EmailUpdates`, `RemindWorkouts`, `RemindMeals`. These default to true in the database for existing rows; `HasSentinel(true)` makes sure EF still saves `false`. My account has a **Notifications** panel with switches: clients get all five; trainers get appointments plus "New clients assigned to me"; professionals get appointments; admins don't see the panel.
- `Helpers/TimeAgo.cs`, `Models/ViewModels/NotificationViewModels.cs`, and new views `Account/ForgotPassword`, `Account/ResetPassword`, `Account/ConfirmEmail`, `Notifications/Index`, `Shared/Components/NotificationBell/Default`.
- Migration `20261020074032_Notifications`.

### Changed
- `appsettings.json`: the connection string password is back to the `YOUR_PASSWORD` placeholder. The real password had been committed in `ba40a85`; user secrets already provide it in Development. Added the `Email` and `Reminders` sections.
- `_LoginPartial`: the bell and account menu are now grouped together. `_Layout`: the email-confirmation banner.
- Registration success message mentions the confirmation email.
- `AppointmentsController`, `WorkoutPlansController`, `DietPlansController`, `ProgressController`, `TrainerAssignmentsController`, `UsersController`: inject `NotificationService` / `AccountEmails`.
- `site.css`: `.ht-bell`, `.ht-bell-count`, `.ht-bell-menu`, `.ht-bell-item`, `.ht-bell-text`, `.ht-unread`, `.ht-icon-sm`.
- README:
  - Mailpit in prerequisites and a new step 4 in Getting Started.
  - Email in the tech stack.
  - A new "Emails and notifications" section.
  - Progress ticked.

### Environment / verification
- Build: succeeded with 0 warnings and 0 errors. The migration applied on startup.
- Ran on port 5299 with `Reminders__CheckIntervalMinutes=1`, `WorkoutTime`/`MealTime=00:00`, `WindowHours=23` and `Email__AppUrl=http://localhost:5299`. Emails were checked through the Mailpit API.
  - **Account emails** (temporary client `t2@healthtrack.local`):
    - Registration sends a confirmation email and shows the banner.
    - A bad confirmation code → "Confirmation failed". Resend → a new email; the link → "Email confirmed" and the banner disappears.
    - Forgot password for an unknown and a known email shows the same page, and only the known one gets an email.
    - A bogus token and a reused token → "invalid or has expired". A valid reset → the old password fails and the new one works.
    - A password change sends the notice (confirmed address only).
    - An email change sends "email was changed" to the old address and a confirmation to the new one, and shows the banner and "Not confirmed yet".
  - **Notifications:**
    - Admin assigning the trainer → the client and trainer are notified and emailed.
    - Trainer creating a plan → the client is notified.
    - Booking → the trainer gets "New appointment request". Approving → the client gets "Appointment approved".
    - Within a minute the reminder service sent "Upcoming appointment" to both. Later ticks didn't duplicate it (one row per key).
    - The seeded client got a real workout reminder from the existing plan.
    - With `EmailUpdates` off, feedback created an in-app notification but no email. The client cancelling → the trainer was emailed.
    - The bell showed the unread count and the latest items. `Open` redirected to `/Progress#feedback` and marked the item read; the trainer opening the client's notification → 404. Mark all read → 0 unread and no badge. Clear read → empty.
    - Preference switches saved `false` correctly (checked in the database).
- Screenshots: the confirmation email as rendered by Mailpit (700px), the Notifications page with the banner and bell (1200px), and the phone header at 380px.
- **Cleanup:**
  - Deleted the temporary user (`t2`, renamed `t3`), its plan, the appointment, all notifications and feedback, and reset those identity seeds. The `WorkoutPlans` seed was reset to the current maximum.
  - Emptied the Mailpit inbox.
  - Stopped only the port-5299 process.
  - Your existing plan ("3 months workout plan") and accounts are untouched.

### Notes
- The old commit `ba40a85` (already on GitHub) still contains the real SQL password. Change the SA password, or rewrite history if that matters for this repo.
- Reminders use the server's local time, like the rest of the app.
- Emails are queued in memory, so anything still queued is lost if the app stops.
- For a real provider, set `Email:Host`, `Port`, `UseSsl` and credentials in user secrets or environment variables, and `Email:AppUrl` to the public URL.

## 2026-10-20 — My account page

Commit: not committed yet

### Added
- **My account** (`/Account/Manage`, any signed-in user):
  - The header shows your role, member-since date and, for clients, your trainer.
  - **Profile form** (`UpdateProfile`): full name, email, date of birth and gender, with the same validation as registration.
    - Changing your email needs your current password, and the email is also your username. A duplicate email shows "already taken".
    - The sign-in cookie is refreshed after saving, so you stay signed in.
  - **Change password form** (`ChangePassword`): current, new and confirm, using Identity's `ChangePasswordAsync` so the password rules apply. A wrong current password shows "Your current password is incorrect." You stay signed in afterwards.
  - Both forms log to the app log (`User {UserId} changed their email/password`).
- `Models/ViewModels/ManageAccountViewModels.cs` (`ProfileFormViewModel`, `ChangePasswordViewModel`, `ManageAccountViewModel`) and `Views/Account/Manage.cshtml`.

### Changed
- The account dropdown has a **My account** item (`bi-person-gear`).
- The dashboard profile card has an **Edit** link.
- README: "Who can do what" mentions the account page.

### Environment / verification
- Build: succeeded with 0 warnings and 0 errors. No migration.
- Curl on port 5299 with a temporary registered client (`t1@healthtrack.local`):
  - Signed out → Login. The form prefills the profile.
  - Email change without, or with a wrong, current password → error. Changing to `client@healthtrack.local` → "already taken". A future date of birth and gender `Robot` → errors.
  - Name, date of birth and gender saved. Email changed to `t1b@…` with the password, and still signed in.
  - Password change: a wrong current password → error, a weak password → rules error, a valid change → still signed in.
  - Sign-in with the old email → fails. The new email with the old password → fails. The new email with the new password → works.
  - Trainer, professional and admin each get 200 on the page.
- Screenshot of the page at 1200px.
- **Cleanup:** deleted the temporary user and stopped only the port-5299 process.

## 2026-10-20 — Progress tracking & analytics, plus earlier gaps

Commit: not committed yet

### Added
- **Data model** (migration `20261020070542_ProgressAndAdmin`):
  - `ApplicationUser.LastLoginAt` (UTC, nullable), set on sign-in and registration.
  - `ProgressFeedback`: client, author, `Message` (1000), `CreatedAt` (UTC). Both FKs to `AspNetUsers` use `NoAction` (two paths to the same table). Index on `(ClientId, CreatedAt)`.
- **Health professional role** (`Roles.Professional`, shown as "Health professional" via `Roles.DisplayName`):
  - Can register (third option on the Register page, `bi-hospital`) and is seeded in Development as `doctor@healthtrack.local` / `Doctor@12345` (Dr. Sam Patel).
  - `Roles.Providers` (Trainer + Professional) is used for the appointment provider actions.
  - New `Dashboard/Professional`: tiles for appointments today (with next time), pending requests, completed this month and distinct clients seen; panels for Today, Coming up (next 5 after today), requests (with Approve/Decline) and the profile card.
- **Progress page** (`ProgressController`, `/Progress`, Client + providers):
  - Clients always see their own data (any `id` is ignored). Trainers see their assigned clients. Professionals see clients they have an approved or completed appointment with (`ClientAccess.VisibleClients`). Anyone else gets 404. Providers without an `id` are redirected to Clients or Appointments.
  - Ranges: 30 / 90 / 180 / 365 days (`Helpers/Periods.cs`; anything else falls back to 90).
  - Tiles: current weight with change over the period, BMI with category, workouts (minutes and kcal burned), average kcal eaten per logged day with the diet plan target. The subtitle shows active days out of the period.
  - Charts: weight, BMI (height carried forward, including from before the range), workout minutes and calories burned (daily for 30 days, weekly otherwise), calories eaten (daily up to 90 days, weekly average per logged day beyond) with the active diet plan's target as a dashed line, plus body fat and resting heart rate when there's data. Several measurements on one day keep the last one.
  - Top 5 activities with a meter bar.
  - Feedback: providers who can see the client post feedback (5–1000 characters). Clients see it with the author's name, role and time. Authors can delete their own; anyone else gets 404.
- **Reports** (`ReportsController`, `/Reports`, Trainer + Admin; ranges 30/90/180/365 days, default 30):
  - Scope: trainers see their own clients, plans and appointments; admins see all.
  - Tiles: active clients / total, average active days per client per week, entries logged, appointments completed.
  - Client performance table: workouts, minutes, kcal burned, average kcal eaten, weight change, active days, last activity (all time) and an engagement badge (High ≥3 active days/week, Medium ≥1, Low >0, Inactive). Trainers' client names link to the client's Progress page; admins also see each client's trainer.
  - Engagement chart (active clients per day, or per week above 30 days), appointments by status with meter bars, the 8 most common plan exercises (plans and clients, all time) and the 8 most logged activities (sessions, minutes, clients).
  - `Reports/Export`: the client table as a UTF-8 CSV (with BOM, so Excel opens it correctly). Text cells starting with `= + - @` are prefixed with `'` to stop formula injection; numbers are left alone.
- **User management** (`UsersController`, `/Users`, Admin only):
  - List with name/email search and role filter (up to 200 rows), showing role, trainer, joined, last sign-in and status (Active / Locked / Disabled).
  - Create a user with any role (including Admin), with a password. Email is marked confirmed.
  - Edit name, email (also the username), role, date of birth and gender. Role changes are blocked for your own account, for a trainer who still has assigned clients, and for anyone with upcoming pending or approved appointments. Moving someone out of the Client role clears their trainer.
  - Reset password, using Identity's reset token so the password rules apply.
  - Disable (lockout until `DateTimeOffset.MaxValue`) or enable/unlock. You can't disable yourself.
  - Role changes, disabling and password resets update the security stamp. `SecurityStampValidatorOptions.ValidationInterval` is now 1 minute, so existing sessions pick up the change within a minute.
  - `Helpers/AccountStatus.cs`: disabled/locked checks, label and badge.
- **Editable log entries**: `Edit` actions on `WorkoutLogs`, `MealLogs` and `Measurements`. `Index?edit={id}` loads the entry into the form ("Edit workout", "Save changes" and Cancel), with the row highlighted (`.ht-editing`). Edit (pencil) links sit beside the delete buttons. Only the owner's entries can be edited (404 otherwise). An unchanged date older than a year is still accepted, so old entries stay editable. A measurement edited with a blank height keeps its previous height.
- **Charts**: Chart.js 4.4.1 copied to `wwwroot/lib/chart.js` (with its licence), `wwwroot/js/charts.js`, and the `_Chart` / `_ChartScripts` partials. Chart data is embedded as JSON (`ChartData` / `ChartSeries` records) and every chart has a "Show data" table. Single-axis charts only, 2px lines, rounded bars, an index tooltip, a legend only for several series, whole-number ticks for counts, and no animation when the user prefers reduced motion. Colours: brand orange first, then blue, aqua and violet for the stacked usage chart (checked with a colour-blindness palette validator; aqua is below 3:1 contrast, so that chart has a totals list and a data table).
- New partials: `_EditLink`, `_TrackingSubmit`, `Users/_UserFields`. New view models: `ProgressViewModels.cs`, `ReportViewModels.cs`, `UserViewModels.cs`, `ChartViewModels.cs`.

### Changed
- **Appointments:** clients can book trainers or health professionals. The dropdown is grouped into "Trainers" and "Health professionals" and labelled "Trainer or health professional". All provider actions (approve, decline, edit, complete, cancel) are open to both roles. `IsTrainer` was renamed `IsProvider` in the appointment controller, view models and views. Details pages say "With", "Note from {name}" and "Waiting for {name}…". Providers get a "Health progress" link to the client once an appointment is approved or completed. The `Appointment.TrainerId` column was kept and now holds whichever provider was booked.
- **Admin dashboard:**
  - Six tiles: total, active this week, clients, trainers, professionals, locked/disabled.
  - A stacked "System usage · last 14 days" chart (workouts, meals, measurements, appointments created per day).
  - A totals list with "+this week".
  - Recent registrations shows each user's last sign-in and has an "All users" link. The header has "New user" and "Reports" buttons.
- **Client dashboard:** a "Progress" button and a "Latest feedback" panel (latest 2, clamped to 3 lines).
- **Trainer dashboard:** a "Reports" button. **Client details:** a "Progress & feedback" button.
- **Navigation:**
  - Admin gets Users and Reports. Trainer gets Reports, and Clients stays highlighted on a client's Progress page. Professionals get Appointments.
  - The client's Track dropdown has a Progress item.
  - The account pill shows "Health professional".
- **Measurements page:** a "See trends" button. `_MeasurementTable` takes `CanManage` and `EditingId` instead of `CanDelete`.
- **Sign-in:** a disabled account now gets "This account has been disabled. Please contact an administrator." instead of the 15-minute lockout message.
- `TrackingRules.ValidateDate` takes an optional original date. `MemberDashboardViewModel.FirstName` skips titles like "Dr.", so the greeting says "Hi, Sam" rather than "Hi, Dr.".
- `site.css`: `.ht-chart`, `.ht-chart-data`, `.ht-meter`, `.ht-range`, `.ht-editing`, `.ht-clamp`, `.ht-usage-week`.
- README: health professional seed account, Appointments wording, new "Progress tracking & analytics" and "User management" sections, "Who can do what" table, Chart.js in the tech stack, progress ticked.
- CHANGELOG: filled in the commit hash (`d3f5e75`) for the appointment entry.

### Fixed
- **Seeding silently skipped accounts.** The configuration binder dropped any `SeedUsers` entry that left out an optional field (`DateOfBirth`, `Gender`, `TrainerEmail`), because the `SeedUser` record's constructor parameters had no defaults. Only the client entry, which sets every field, was being bound; the admin and trainer only existed because an earlier version of the seeder created them. The optional parameters now default to `null`.
- **Chart curve smoothing turned off** (`tension: 0`). The smoothing drew bumps between points that weren't in the data (resting heart rate looked like it rose above 63).

### Environment / verification
- Build: succeeded with 0 warnings and 0 errors. `dotnet ef migrations add ProgressAndAdmin`; applied automatically on startup. Docker Desktop was started (SQL Server wasn't running).
- Ran on port 5299 and tested with curl (`ht.sh` in the session scratchpad), signed in as all four seeded accounts plus a temporary account:
  - **Access sweep:**
    - Every new page returns 200 for the roles allowed to use it. Reports, Users and the Professional dashboard return Access Denied for other roles.
    - Admin → Progress and Appointments → Access Denied. Providers without an id → redirected.
  - **Progress (seeded data posted through the forms):**
    - 13 measurements, 23 workouts and 63 meals. Tiles showed 66.2 kg / −5.7 kg, BMI 23.5 "Healthy weight", 23 workouts, 1,738 kcal average.
    - All seven charts were present. The 7-day range falls back to 90 days.
  - **Feedback:**
    - A short message fails validation. The trainer's feedback shows on the client dashboard.
    - The professional gets 404 on the client's Progress page and on posting feedback until the client books them and they approve. After that: 200, a "Health progress" link on the appointment, and their feedback shows as "Health professional".
    - The professional deleting the trainer's feedback → 404. A client posting feedback → Access Denied.
  - **Log editing:**
    - Workout edit form prefills and highlights the row. A future date → error. Saving → updated.
    - Meal edit works, and `Brunch` is rejected. Measurement edit keeps the height (168.0).
    - The trainer and a second client trying to edit the client's entries → Access Denied / 404. A missing id → 404.
  - **Users:**
    - Create validation: missing password, invalid role, duplicate email, weak password.
    - Created `c2@healthtrack.local`, who could sign in.
    - Role changes blocked as expected: trainer → client (has a client), professional → client (upcoming appointment), admin self-change, admin self-disable. A duplicate email on edit → error.
    - Changing `c2` from Client to Trainer: their old session still worked straight away and was signed out after 65 seconds. Signing in again landed on the trainer dashboard.
    - Disable → the disabled-account message at sign-in and a "Disabled" badge. Enable → works.
    - Password reset with a mismatch → error. After the reset, the old password fails and the new one works.
  - **Reports:** trainer tiles 1/1, 4.4 days/week, 44 entries. CSV export for trainer and admin (admin includes the Trainer column). After the fix, negative weight change exports as `-5.7`.
  - **Admin dashboard:** 6 users, 5 active, usage chart and totals correct.
- Screenshots (headless Chrome over the DevTools protocol, with reduced motion emulated):
  - At 1200px: client Progress (30 and 90 days), admin dashboard, trainer Reports, professional dashboard, Users list, user edit, workout log in edit mode, Register.
  - At 420px: client Progress.
- **Bugs found and fixed while testing:**
  - The seeding bug above.
  - CSV numbers were being prefixed with `'`.
  - Count axes showed decimal ticks.
  - The curve overshoot.
  - The "Hi, Dr." greeting.
  - Screenshots caught charts mid-animation (headless Chrome doesn't run animation frames reliably); the screenshot script now emulates reduced motion.
- **Cleanup:**
  - Deleted all test workouts, meals, measurements, the appointment and the feedback, and reset those identity seeds.
  - Removed `c2@healthtrack.local`.
  - Kept the existing workout plan and `johndoe@gmail.com`. The seeded accounts' `LastLoginAt` values from testing remain.
  - Stopped only the port-5299 process and headless Chrome. A temporary startup log that had dumped configuration was deleted.

### Notes
- Email confirmation is still off. It needs a real email sender (SMTP or a provider), which also suits the Notifications step.
- The `Appointment.TrainerId` / `TrainerNote` names were kept to avoid a rename migration; they now mean "provider".
- Admins see aggregated reports and client names but can't open an individual client's Progress page, which keeps detailed health data with the client's trainer and professionals.
- Accounts are disabled, not deleted, because plans, appointments and feedback reference users with `NoAction` FKs.
- Usage per day is based on when entries were created (`CreatedAt`), not the date they were logged for.

## 2026-10-04 — Appointment system

Commit: `d3f5e75`

### Added
- **Data model** (migration `20261004063449_Appointments`):
  - `Appointment`: client, trainer, `StartsAt`/`EndsAt` (`datetime2`, server local time), `Mode`, optional `Location` (200), `Reason` (300), `Status`, optional `TrainerNote` (300), `CancelledByClient`, `CreatedAt`/`UpdatedAt` (UTC), and a computed `DurationMinutes` that isn't stored.
  - `AppointmentStatus` enum (Pending, Approved, Declined, Cancelled, Completed) and `AppointmentMode` enum (InPerson, Video, Phone), both stored as strings (max 10).
  - Client and trainer FKs use `NoAction`, as with plans, to avoid two cascade paths to `AspNetUsers`. Indexes on `(TrainerId, StartsAt)` and `(ClientId, StartsAt)`.
- `Helpers/AppointmentRules.cs`: allowed durations (30/45/60/90), opening hours (06:00–21:00), 90-day booking limit, the `CanRespond` / `CanReschedule` / `CanCancel` / `CanComplete` checks shared by the controller and views, status labels (a pending request whose start time has passed shows as "Expired"), and `Label()`/`Icon()` extensions for the mode.
- `Models/ViewModels/AppointmentViewModels.cs`: a shared `AppointmentSlotViewModel` (date, time, duration, type) with `AppointmentRequestViewModel` (adds trainer and reason) and `AppointmentEditViewModel` (adds location and note), plus page models.
- `AppointmentsController` (`/Appointments`, Client and Trainer; Admin gets Access Denied):
  - `Index`: Clients see a booking form (trainer dropdown with their assigned trainer preselected) and three lists: Awaiting approval, Upcoming, and Past & cancelled (latest 50). Trainers see Requests to review (with a count badge), Upcoming, and Past & cancelled, with inline Approve / Decline buttons for requests and Complete for past approved ones.
  - `Create` (Client): validates the trainer is a real Trainer, the slot (future, within 90 days, inside opening hours, listed duration, valid type), and overlaps. It is rejected if the trainer has an approved appointment at that time ("Alex Morgan is already booked…") or the client already has a pending or approved one.
  - `Details/{id}`: only the appointment's client or trainer can view it; anyone else gets 404. Shows when, duration, type, location, reason, trainer note, request time, and who cancelled. The Actions panel shows only what's allowed: Approve; Decline with an optional reason; "Change time & approve" or "Reschedule or edit"; Mark as completed; Cancel (trainers can add a reason).
  - `Approve` refuses if the trainer already has an approved appointment that overlaps, showing a red error alert. `Decline` stores the optional note. `Cancel` records whether the client cancelled. `Complete` works only after the start time. Every action checks ownership and state and returns 404 otherwise. Inline actions take a `returnUrl` that must be local (`Url.IsLocalUrl`).
  - `Edit/{id}` (Trainer): reschedule date, time, duration and type, and set a location or meeting link and a note. Saving re-runs the slot and overlap checks (excluding the appointment itself) and confirms it as Approved.
- Views: `Appointments/Index`, `Details`, `Edit`, `_SlotFields` (date/time/duration/type fields, rendered with `<partial for="Form">` so names keep the `Form.` prefix), and shared `_AppointmentList`, `_AppointmentItem` and `_AppointmentStatus` partials.
- CSS: `.ht-badge-pending` (amber), `.ht-badge-completed` (blue), `.ht-note`, `.ht-min-w-0`, `.ht-appt-reason`.

### Changed
- **Client dashboard:** the "Upcoming appointments" panel now lists the next 3 pending or approved appointments, with a "Book" or "View all" link.
- **Trainer dashboard:**
  - The "Appointments today" tile counts today's approved and completed appointments, with a hint of "Next at HH:mm", "All done for today" or "None scheduled".
  - The "Appointment requests" panel lists the next 5 pending requests, with Approve and Decline buttons.
- **Layout:**
  - New "Appointments" nav link (`bi-calendar-check`) for clients and trainers.
  - New red alert for `TempData["Error"]`, styled like the success alert.
  - The navbar now collapses below `lg` (992px) instead of `md`. With six links, the client's account pill was cut off at 800px. Updated `navbar-expand-lg`, `ms-lg-3 gap-lg-1`, `order-lg-last` in `_LoginPartial`, and the two navbar media queries in `site.css` (now `max-width: 991.98px`).
- README: added an Appointments section, updated "Who can do what" and the Helpers line, and ticked "Appointment system".
- CHANGELOG: filled in the commit hash (`68caada`) for the workout & diet entry.

### Environment / verification
- Build: succeeded with 0 warnings and 0 errors. `dotnet ef migrations add Appointments` created the table, FKs and indexes; the migration applied on startup.
- Ran on port 5299 and tested with curl, signed in as the seeded client, the seeded trainer, the admin, and a temporary second client:
  - Valid request → 302; the trainer sees it under Requests.
  - Rejected with field errors: a request overlapping the client's own; a past time; 05:00 start; 20:30 + 60 min (past closing); a 20-minute duration; an invalid type; a short reason; an unknown trainer; 120 days ahead.
  - A second client can request an overlapping slot while the first is only pending. Approving it after the first was approved → "You already have an appointment at that time" alert.
  - Decline with `returnUrl=https://evil.com` → redirected to the Details page instead. The decline note shows to the client.
  - Client POST to Approve → Access Denied. Another client opening someone else's appointment → 404. Admin → Access Denied.
  - Trainer edit: the form prefills `10:00`, `60 min` and `Video`. Moving it to 14:00 for 45 min with a location and note → client sees Approved, 14:00–14:45, "Studio 2", the note. A booking at 14:30 is rejected ("Alex Morgan is already booked…"); 14:45 (back-to-back) is accepted. Rescheduling another request onto 14:00 → conflict error.
  - Client cancels → "Cancelled by Jamie Silva". Cancelling again → 404. Complete on a future appointment → 404.
  - Past rows inserted with SQL: a past approved appointment shows a Complete button, and completing it shows "Completed". A past pending one shows "Expired"; approving or cancelling it → 404.
- Screenshots (headless Chrome over the DevTools protocol):
  - At 1200px: client Appointments, trainer Appointments, trainer dashboard, appointment details, edit form.
  - At 420px: trainer Appointments (buttons wrap below the item) and client dashboard.
  - Client navbar at 1000px (fits on one row) and at 800px (collapsed menu open).
- **Cleanup:** deleted all test appointments, reset the identity seed, and removed the temporary client `c2@healthtrack.local`. Stopped only the port-5299 process and headless Chrome. The user's `dotnet watch` on 5182 was left running.

### Notes
- There's no separate "healthcare professional" role yet; consultations are booked with trainers. A new role could reuse the same flow.
- Pending requests don't block a slot. Only approved appointments do, so a trainer can receive competing requests and approve one.
- Times use the server's local clock (`DateTime.Now`), matching how the rest of the app handles dates.
- Appointment reminders belong to the "Notifications" step.

## 2026-10-04 — Workout & diet management

Commit: `68caada`

### Added
- **Data model** (migration `20261004054823_WorkoutAndDiet`):
  - `ApplicationUser.TrainerId` / `Trainer`: the client's assigned trainer. Self-referencing FK with `NoAction` on delete.
  - `WorkoutPlan`: title, description, start/end date, `IsActive`, `ClientId`, `TrainerId`, plus a list of `WorkoutExercise` (optional `Day` stored as a string; name, sets, reps, minutes, notes).
  - `DietPlan`: same fields plus `DailyCalorieTarget`, with a list of `DietMeal` (`MealType` stored as a string; description, calories).
  - `WorkoutLog` (date, activity, minutes, calories burned, notes), `MealLog` (date, meal type, description, calories), `HealthMeasurement` (date, weight kg, height cm, body fat %, resting heart rate, notes, plus a computed `Bmi` that isn't stored).
  - Plan → client/trainer FKs are `NoAction`, because SQL Server rejects two cascade paths to `AspNetUsers`. Plan → items cascade, and log → client cascades.
  - Indexes on `(ClientId, Date)` for the three log tables. Decimal precision 5,1 for weight/height and 4,1 for body fat.
- `MealType` enum (Breakfast, Lunch, Dinner, Snack).
- `Helpers/Dates.cs` (today, Monday-based start of week, age, Monday–Sunday order) and `Helpers/Bmi.cs` (BMI category).
- `ApplicationDbContext.UsersInRole(role)`: an `IQueryable` filter used for the role counts and lists.
- **Admin:** `TrainerAssignmentsController` (`/TrainerAssignments`, Admin only) lists every client with a trainer dropdown (or Unassigned) and Save button, plus a trainer panel with each trainer's client count. `Assign` checks that the user is a Client and the chosen user is a Trainer (otherwise 404/400) and logs the change.
- **Trainer:**
  - `ClientsController` (`/Clients`, Trainer only): a table of assigned clients with latest weight, workouts this week, active plans and last activity. `Details/{id}` shows the client's profile, workout and diet plans, last 10 measurements, workouts and meals, and "+ Workout plan" / "+ Diet plan" buttons. A client who isn't assigned to the trainer gives 404.
  - `WorkoutPlansController` and `DietPlansController`, shared by Client and Trainer roles:
    - `Index`: trainers see the plans they created, with a client filter; clients see plans made for them.
    - `Details`: the plan's trainer or client can view it. Exercises are grouped by day ("Any day" first, then Monday–Sunday, with today marked); meals are grouped by type, with stat tiles for daily target, planned total and meal count.
    - `Create`, `Edit`, `Delete`, `AddExercise`/`DeleteExercise` and `AddMeal`/`DeleteMeal` are trainer-only and limited to the trainer's own plans.
    - Creating a plan, or moving it to a different client, is only allowed for the trainer's assigned clients. Editing keeps the current client even if they were reassigned since.
    - End date can't be before start date. An exercise needs sets/reps or a duration. Calorie target must be 500–10,000.
  - `PlanClients` helper: client dropdown and plan validation shared by both controllers.
- **Client:** `WorkoutLogsController`, `MealLogsController`, `MeasurementsController` (Client only) at `/WorkoutLogs`, `/MealLogs`, `/Measurements`:
  - Each page has an add form, a history of the latest 50 entries, and delete for the client's own entries only.
  - Dates can't be in the future or more than a year ago.
  - Measurements: weight 20–400 kg, height 50–260 cm, body fat 2–75 %, resting HR 25–220. Height is pre-filled from the last entry, and carried forward on save if left blank, so every entry gets a BMI. The page shows tiles for current weight, change since the previous entry, and BMI with its category.
  - Meal history is grouped by day with daily kcal totals.
  - `TrackingRules` helper: date validation, trimming, page size.
- **Views:**
  - Shared: `PlanForm` (create/edit for both plan types; the calorie target field only appears for diet plans), `_PlanList`, `_PlanHeader`, `_PlanStatus`, `_PlanLinks`, `_DeleteButton`, `_MeasurementTable`, `_ClientTable`, `_TrainerLoads`.
  - Per controller: `WorkoutPlans/Index`, `WorkoutPlans/Details`, `DietPlans/Index`, `DietPlans/Details`, `Clients/Index`, `Clients/Details`, `TrainerAssignments/Index`, `WorkoutLogs/Index`, `MealLogs/Index`, `Measurements/Index`.
- `wwwroot/js/site.js`: forms with `data-confirm="…"` ask for confirmation before submitting (used by every delete button).
- View models: `PlanViewModels.cs`, `TrackingViewModels.cs` (including the generic `TrackingPageViewModel<TForm, TEntry>`), `TrainerViewModels.cs`, and `DeleteButton` / `ClientDashboardViewModel` / `TrainerDashboardViewModel` in `DashboardViewModels.cs`.

### Changed
- **Dashboards now use real data:**
  - Client:
    - Tiles: current weight, BMI and category (using the last known height), workouts and minutes this week (from Monday), calories eaten today against the diet plan's target.
    - Panels: "Today's workout" from the active plan (exercises for today or any day; shows "Rest day" if none), the diet schedule, and the last 5 workouts.
    - Quick buttons: Log workout, Log meal, Measurement. The header names the trainer.
  - Trainer: tiles for client count, active plans and client workouts this week; the client progress table; quick "+ Workout plan" / "+ Diet plan" buttons.
  - Admin: the trainer-assignments panel shows "x of y clients assigned", each trainer's client count, and a Manage link.
  - Appointments panels and tiles still show empty states until the appointment system is built.
- `DashboardController`: uses `Dates.Age`; `Client` and `Trainer` return the new view models.
- `_StatTile` and `_EmptyState` moved from `Views/Dashboard/` to `Views/Shared/` so every page can use them. `_ProfileCard` no longer uses `h-100`; it was overflowing past the footer when stacked under another panel.
- **Navbar:**
  - Role-specific links: Trainer gets **Clients** and a **Plans** dropdown; Client gets **Plans** and **Track** (Workouts / Meals / Measurements) dropdowns; Admin gets **Assignments**. The current section is highlighted (`NavActiveAny`).
  - Privacy is no longer in the navbar; it's still in the footer.
- `DbSeeder`: seed users can set `TrainerEmail`. After the users are created, a client with no trainer yet is assigned to that trainer (startup fails if it isn't a Trainer). An existing assignment, for example one an admin changed, is left alone. `appsettings.Development.json` assigns `client@healthtrack.local` to `trainer@healthtrack.local`.
- `_ViewImports.cshtml`: added `Microsoft.AspNetCore.Mvc.Rendering` and `Microsoft.AspNetCore.Mvc.ViewFeatures`.
- `site.css`: new classes `.ht-form-panel`, `.ht-prewrap`, `.ht-badge-active`/`-inactive`, `.ht-group-title`, `.ht-group-meta`, `.ht-item-list`, `.ht-icon-btn`, `.ht-date-chip`, `.ht-meal-badge`, `.ht-assign-form`, plus navbar dropdown and breadcrumb colours.
- `README.md`: ticked "Workout & diet management"; added a "Who can do what" table, a note that the seeded client is assigned to the seeded trainer, and `Helpers/` in the project structure.

### Environment / verification
- Build: succeeded with 0 warnings and 0 errors. `dotnet ef migrations add WorkoutAndDiet` was applied automatically on startup.
- Scripted with curl on port 5299 (`wd.sh` and `sweep.sh` in the session scratchpad), signed in as the three seeded accounts:
  - **Access:**
    - Client → `/Clients`, `/WorkoutPlans/Create`, `Edit` or `Delete` → Access denied.
    - Trainer → `/WorkoutLogs` and `/TrainerAssignments` → Access denied.
    - Admin → `/WorkoutPlans` → Access denied.
    - Signed out → Login.
    - Trainer posting a delete for a client's log → Access denied.
    - The client's plan page shows no edit controls.
  - **Validation:**
    - Empty plan / end before start → three errors.
    - Plan for a client who isn't the trainer's → "You can only create plans for your own clients."
    - Exercise with no sets or duration → error.
    - Meal type `Brunch` → rejected.
    - Calorie target 100 → range error.
    - Future workout date → "Date can't be in the future."
    - Weight 5 / height 400 → range errors.
  - **Flows:**
    - Created a workout plan with three exercises (today, any day, Friday) and a diet plan with meals.
    - Logged workouts, meals and measurements.
    - The client dashboard showed 67.2 kg, BMI 24.7 "Healthy weight", workouts, 970 kcal today against a 2,000 target, today's two exercises, the diet meals, and "Your trainer is Alex Morgan".
    - The trainer saw the client's logs, measurements and plans.
    - Admin unassigned, then reassigned, the client: while unassigned the trainer got 404 on the client page but could still open their own plan. Assigning a non-trainer → 400.
    - Deleting a plan removed it (404 afterwards).
  - Requested every page as each role after the fixes: all 200.
- **Bugs found and fixed while testing:**
  - The admin dashboard returned 500. EF Core couldn't translate `OrderBy` applied after projecting into the `TrainerLoad` record. The query now sorts before projecting.
  - A measurement saved without height had no BMI. Height is now carried forward.
  - The diet plan showed kcal twice when a group had one meal, and group totals were uppercased by the heading style (`.ht-group-meta` fixes this).
  - The profile card overflowed the footer.
- Screenshots (headless Chrome over the DevTools protocol): client dashboard, trainer workout plan page, client diet plan page, measurements, trainer client details, admin assignments, workout plan list, new-plan form, the Track dropdown at 1200px, the meal log at 420px, and the phone menu with the Track dropdown open.
- **Cleanup:** deleted all test plans, exercises, meals, logs and measurements; the tables are empty. The seeded client→trainer assignment remains. Stopped only the port-5299 process and headless Chrome.

### Notes
- Log entries can be added and deleted but not edited.
- When a client is reassigned, their old trainer keeps access to the plans they wrote (they own them) but can no longer see the client's page or create new plans for them.
- Trainer feedback on client progress isn't built yet; it fits with "Progress tracking & analytics".
- Charts/trends of weight and BMI over time are also left for "Progress tracking & analytics"; this step stores the data they will use.

## 2026-10-04 — Redesigned top bar

Commit: `8cf85a9`

Replaces the separate Dashboard button, username text and Logout button from the previous entry, which looked cluttered.

### Changed
- `HealthTrack/Views/Shared/_Layout.cshtml`:
  - Nav links now have icons: Home (`bi-house`), Dashboard (`bi-speedometer2`, only when signed in) and Privacy (`bi-shield-check`).
  - The current page's link is highlighted. A `NavActive(controller, action?)` helper at the top of the layout compares the route values and adds `active`; any Dashboard page highlights Dashboard.
  - `_LoginPartial` moved outside the collapsing menu, between the brand and the toggle. `order-md-last` keeps it on the far right on desktop.
  - Removed the phone-only Dashboard button.
- `HealthTrack/Views/Shared/_LoginPartial.cshtml` — rewritten:
  - Signed in: a pill-shaped account button showing a white avatar with up to two initials, plus the full name and role (Admin/Trainer/Client). The name and role are hidden on phones, leaving just the avatar.
  - Clicking it opens a dropdown with the full name and email, then "Dashboard" and "Sign out". Sign out is still a POST form with the anti-forgery token.
  - Signed out: "Login" link and white "Register" button, visible without opening the menu on phones too.
  - Loads the user with `UserManager.GetUserAsync` to show the full name instead of the email.
- `HealthTrack/wwwroot/css/site.css` — new or changed rules:
  - `.ht-navbar .nav-link`: rounded pill padding; translucent white background on hover and when active; bold when active.
  - `.ht-nav-login`.
  - `.ht-user-toggle`: translucent pill with a white border and a focus outline.
  - `.ht-avatar`, `.ht-user-text`, `.ht-user-name`, `.ht-user-role`.
  - `.ht-user-menu`: rounded dropdown with an orange-tinted shadow, orange icons, and orange-light hover on items.
  - Extra vertical padding for the expanded menu on phones.

### Environment / verification
- Build: succeeded with 0 warnings and 0 errors.
- Headless Chrome screenshots (fresh profile, port 5299):
  - Signed out at 1200px and 420px: Login and Register visible on both; Home highlighted.
  - Signed in as `trainer@healthtrack.local` at 1200px: Dashboard highlighted; "AM · Alex Morgan · Trainer" pill on the right; dropdown open showing name, email, Dashboard and Sign out.
  - At 420px: avatar pill next to the hamburger; expanded menu shows Home / Dashboard (highlighted) / Privacy; dropdown open and fitting on screen.
- Sign out from the dropdown (curl): POST `/Account/Logout` → 302 to Home; `/Dashboard` afterwards → 302 to Login.
- Stopped only the port-5299 process and the headless Chrome instance.

## 2026-10-04 — Dashboard button in the navbar

Commit: `8cf85a9`

### Changed
- `HealthTrack/Views/Shared/_LoginPartial.cshtml`:
  - When signed in, a white "Dashboard" button (`btn-light`, `bi-speedometer2` icon) now sits before the username on desktop (`d-none d-md-block`).
  - Logout is now an outline button (`btn-outline-light`), so Dashboard is the only solid button and stands out.
- `HealthTrack/Views/Shared/_Layout.cshtml`:
  - Removed the plain "Dashboard" text link from the left nav list. It looked the same as Home and Privacy and was hidden inside the collapsed menu on phones.
  - On phones (`d-md-none`) the same Dashboard button sits next to the menu toggle, outside the collapsed menu, so it's always visible.

### Environment / verification
- Build: succeeded with 0 warnings and 0 errors.
- Ran on port 5299, signed in as `client@healthtrack.local` and took headless Chrome screenshots of the navbar:
  - At 1200px: Dashboard button, username, then outlined Logout.
  - At 420px: Dashboard button next to the hamburger with the menu collapsed.
- Signed out (curl): no Dashboard button; only Login and Register appear.
- Stopped only the port-5299 process and the headless Chrome instance.

### Notes
- The Home page's "Go to dashboard" button stays.

## 2026-10-04 — Seed one account per role

Commit: `8cf85a9`

### Changed
- `HealthTrack/appsettings.Development.json` — replaced the `SeedAdmin` section with a `SeedUsers` array (`Email`, `Password`, `FullName`, `Role`, optional `DateOfBirth` and `Gender`):
  - Admin: `admin@healthtrack.local` / `Admin@12345`, "System Administrator".
  - Trainer: `trainer@healthtrack.local` / `Trainer@12345`, "Alex Morgan", born 1990-03-15, Male.
  - Client: `client@healthtrack.local` / `Client@12345`, "Jamie Silva", born 1998-07-22, Female.
- `HealthTrack/Data/DbSeeder.cs`:
  - Binds `SeedUsers` to a private `SeedUser` record and seeds each entry.
  - A missing user is created with `EmailConfirmed = true`. If creation fails, startup throws with Identity's error messages.
  - Assigns the configured role if the user doesn't have it yet, which also repairs an existing account that is missing its role.
  - Throws if a seed entry has a role that isn't Client, Trainer or Admin.
  - No `SeedUsers` section (for example outside Development) means no accounts are seeded; roles are still created.
- `README.md` — new "Seeded accounts" section with a table of the three logins; the migrations step now points to it.

### Environment / verification
- Build: succeeded with 0 warnings and 0 errors.
- Started the app in Development on port 5299. The existing admin was kept; the trainer and client were created.
- Signed in as each account with curl: each went to `/Dashboard` and then to its own dashboard (`/Dashboard/Admin`, `/Dashboard/Trainer`, `/Dashboard/Client`). The client dashboard showed Jamie Silva, age 28, Female.
- `sqlcmd`: each seeded account has the right role, date of birth, gender and `EmailConfirmed = 1`. Your `johndoe@gmail.com` (Client) is unchanged.
- Restarted the app: still 4 users and 4 role assignments, so seeding doesn't create duplicates.
- Stopped only the port-5299 process.

### Notes
- If you change a seeded account's password in the app, a restart won't reset it; the seeder only creates missing accounts and adds missing roles.

## 2026-10-04 — Role-based dashboards

Commit: `8cf85a9`

### Added
- `HealthTrack/Controllers/DashboardController.cs` (`[Authorize]`):
  - `Index` sends the user to their role's dashboard: Admin → `Admin`, Trainer → `Trainer`, Client → `Client`. A user with no role goes to Access denied.
  - `Client` (`[Authorize(Roles = Client)]`) and `Trainer` (`[Authorize(Roles = Trainer)]`) load the signed-in user's profile: name, email, age worked out from date of birth, gender, and member-since date. If the user no longer exists, they are sent to sign in (`Challenge`).
  - `Admin` (`[Authorize(Roles = Admin)]`) queries the database for: total users, client and trainer counts (grouped from `AspNetUserRoles`), registrations in the last 7 days, currently locked-out accounts (`LockoutEnd` in the future), and the 8 most recent registrations with their role.
- `HealthTrack/Models/ViewModels/DashboardViewModels.cs` — `StatTile`, `EmptyState` and `RecentUser` records, plus `AdminDashboardViewModel` and `MemberDashboardViewModel` (with a `FirstName` helper for the greeting).
- `HealthTrack/Views/Dashboard/`:
  - `Client.cshtml` — "Hi, {first name}" header with role badge and date. Tiles for current weight, BMI, workouts this week and upcoming appointments. Panels for workout plan, diet schedule and upcoming appointments, plus a profile card.
  - `Trainer.cshtml` — tiles for my clients, active plans, pending requests and appointments today. Panels for client progress and appointment requests, plus a profile card.
  - `Admin.cshtml` — "System overview" with live tiles (total users + new this week, clients, trainers, locked accounts), a recent-registrations table (name, email, role badge, join date) and a trainer-assignments panel.
  - Partials: `_StatTile`, `_EmptyState`, `_ProfileCard`.

### Changed
- `HealthTrack/Controllers/AccountController.cs` — after registering or signing in (with no `returnUrl`), and when opening Login/Register while already signed in, users now go to `/Dashboard` instead of Home. A local `returnUrl` is still followed first.
- `HealthTrack/Views/Shared/_Layout.cshtml` — "Dashboard" navbar link, shown only when signed in.
- `HealthTrack/Views/Home/Index.cshtml` — signed-in users get a "Go to dashboard" button under the welcome-back line.
- `HealthTrack/Views/_ViewImports.cshtml` — added `@using HealthTrack.Models.ViewModels`.
- `HealthTrack/wwwroot/css/site.css` — new classes: `.ht-dash-header` (title row with an orange underline), `.ht-stat` / `.ht-stat-label` / `.ht-stat-value` / `.ht-stat-hint` (stat tiles), `.ht-panel` / `.ht-panel-header` (dashboard sections, no hover lift), `.ht-empty` (dashed orange-tinted empty state), `.ht-profile` (profile definition list), `.ht-table` (muted uppercase headers, orange-tinted row borders).
- `README.md` — ticked off "Role-based dashboards".
- `CHANGELOG.md` — the two previous entries said "not committed yet"; both were included in `ba40a85`, so they now say so.

### Environment / verification
- Build: succeeded with 0 warnings and 0 errors.
- Ran the app in Development on port 5299 and scripted the flows with curl and cookie jars (`dash.sh` in the session scratchpad):
  - Signed out `/Dashboard` → 302 to Login with `ReturnUrl=/Dashboard`.
  - Registered a Client (born 1995-05-10, Female) → 302 to `/Dashboard` → 302 to `/Dashboard/Client` (200). Page shows "Hi, Dash", age 31 and Female.
  - Client opening `/Dashboard/Admin` or `/Dashboard/Trainer` → 302 to Access denied.
  - Registered a Trainer without date of birth or gender → `/Dashboard/Trainer`; profile shows "Not set" for both. Trainer opening `/Dashboard/Client` → Access denied.
  - Seeded admin login → `/Dashboard` → `/Dashboard/Admin`. Tiles showed 4 users / 4 new this week / 2 clients / 1 trainer / 0 locked, matching the database; the table listed all 4 users with the correct roles, newest first.
  - Opening Login while signed in → 302 to `/Dashboard`.
  - Login with `returnUrl=/Home/Privacy` → still redirects to `/Home/Privacy`.
  - Home page, signed in: "Go to dashboard" button and Dashboard nav link present.
- Screenshots: headless Chrome driven over the DevTools protocol with the curl session cookies (`shot.mjs` in the scratchpad). Checked Client, Trainer and Admin at 1200px, and Client and Admin at 420px (tiles go two per row; panels and the profile card stack).
- Cleanup: deleted `dashclient@example.com` and `dashtrainer@example.com`. `admin@healthtrack.local` and your `johndoe@gmail.com` are untouched. Stopped only the port-5299 process and the headless Chrome instance.

### Notes
- Only the admin tiles and table use real data. The client and trainer tiles show `—` / `0` and the panels show empty states, because workouts, diet plans, measurements, appointments and trainer–client assignments don't exist yet. Each will be connected when its feature is built.
- Age is calculated from today's server date and accounts for whether the birthday has happened yet this year.
- Join dates are stored in UTC and shown in the server's local time.

## 2026-10-04 — Center error and success messages

Commit: `ba40a85`

### Changed
- `HealthTrack/wwwroot/css/site.css`:
  - New `.ht-alert` class: flex row with `align-items: center` and a `.6rem` gap, so the icon and text line up vertically. Icons inside it are `1.1rem` and don't shrink.
  - `.ht-alert.alert-dismissible .btn-close` is vertically centred (`top: 50%` with `translateY(-50%)`) instead of sitting at Bootstrap's fixed top padding.
  - `.validation-summary-errors` (the server-side error box on Login and Register) is now a centred flex row with a Bootstrap Icons `exclamation-circle-fill` icon added through `::before` (`content: "\f332"`).
  - `.validation-summary-errors ul` has no margin, padding or bullets. The `<ul>` that ASP.NET generates kept its default `1rem` bottom margin and `2rem` left indent, which pushed the text up and to the right inside the red box. That was the "not centred" look.
- `HealthTrack/Views/Shared/_Layout.cshtml` — the `TempData["Success"]` alert uses `ht-alert`, with a `bi-check-circle-fill` icon and the message in a `<span>`.
- `HealthTrack/Views/Account/Login.cshtml` and `Register.cshtml` — removed `py-2` from the validation summary so it uses the normal alert padding, the same as the success alert.

### Environment / verification
- Ran the app in Development on port 5299 and took headless Chrome screenshots before and after:
  - Login with a wrong password: one error, centred with its icon and no bullet.
  - Register with the weak password `password`: two Identity errors, stacked with no bullets and the icon centred next to them.
  - "You have been signed out." success alert at 1200px and 500px: icon, text and close button are vertically centred.
- Stopped only the port-5299 process.

### Notes
- Field-level messages (the small red text under each input) were already aligned with their inputs and are unchanged.
- The validation summaries use `ModelOnly`, so they only show server-side errors; client-side field errors still appear under each field.

## 2026-10-04 — Switch database from MySQL to SQL Server

Commit: `ba40a85`

### Changed
- `HealthTrack/HealthTrack.csproj`:
  - Replaced `MySql.EntityFrameworkCore` 10.0.9 with `Microsoft.EntityFrameworkCore.SqlServer` 10.0.12 (same version as the other EF Core packages).
  - Added `<UserSecretsId>1dcd3204-09fe-4c30-afdd-cb23ccbb4685</UserSecretsId>` (via `dotnet user-secrets init`).
- `HealthTrack/Program.cs` — `ApplicationDbContext` is registered with `UseSqlServer` instead of `UseMySQL`.
- `HealthTrack/Data/ApplicationDbContext.cs` — removed the `ConfigureConventions` override and the private `DateOnly`↔`DateTime` converter. It only existed to work around the MySQL provider's `InvalidCastException` when reading `DateOnly`; the SQL Server provider maps `DateOnly` to `date` natively.
- `HealthTrack/appsettings.json` — `DefaultConnection` is now a SQL Server template: `Server=localhost,1433;Database=HealthTrack;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True;MultipleActiveResultSets=True`.
- `README.md`:
  - Overview, tech stack (SQL Server 2022 / `Microsoft.EntityFrameworkCore.SqlServer`) and prerequisites (Docker) updated.
  - "Configure the database" step rewritten: `docker run` command for SQL Server 2022, setting the connection string with user secrets, why `TrustServerCertificate=True` is needed, and that the database is created by the first migration.
  - Project structure now lists `Data/` and `Migrations/`; removed the stale `system.txt` entry (the file isn't in the repo).
  - Progress checklist: added "Switched database from MySQL to SQL Server (Docker)".

### Removed / regenerated
- Deleted the MySQL migration `HealthTrack/Migrations/20260927042610_InitialIdentity` (it used `MySQL:Charset` annotations and MySQL column types such as `longtext`, `tinyint(1)`, `datetime(6)`).
- Generated a fresh SQL Server migration with `dotnet ef migrations add InitialIdentity`: `HealthTrack/Migrations/20261004051356_InitialIdentity` plus a new `ApplicationDbContextModelSnapshot.cs`. Same Identity tables and `ApplicationUser` columns as before, now with SQL Server types (`nvarchar`, `bit`, `datetime2`, `datetimeoffset`, `date` for `DateOfBirth`).

### Environment / verification
- Database server: existing Docker container `sql` (`mcr.microsoft.com/mssql/server:2022-latest`, Developer edition, port 1433).
- Connection string with the real `sa` password stored in user secrets (`ConnectionStrings:DefaultConnection`), so the password isn't committed.
- Build: succeeded with 0 warnings and 0 errors.
- Ran the app in Development on port 5299. On startup it created the `HealthTrack` database, applied `20261004051356_InitialIdentity` and seeded the roles and admin. Home returned 200.
- Scripted with curl and a cookie jar:
  - Registered a Client with date of birth `1995-05-10` and gender Male → 302 to Home.
  - Logged out, then logged back in as that user → 302 to Home with "Welcome back". This is the case that failed under MySQL before the converter was added, so it confirms `DateOnly` works without it.
  - Seeded admin `admin@healthtrack.local` login → 302.
- Checked with `sqlcmd` inside the container: `__EFMigrationsHistory` has the one migration; roles `Admin`, `Client`, `Trainer` exist; the test user has role Client, `DateOfBirth` `1995-05-10`, a hashed password (`AQAAAA…`); the admin has role Admin.
- Cleanup: deleted `sqltest@example.com`; only `admin@healthtrack.local` remains. Stopped only the port-5299 process.

### Notes
- Data was not copied from the old MySQL `healthtrack` database (it only held the seeded admin). That database is left as-is and is no longer used.
- Deleting rows by hand with `sqlcmd` needs the `-I` flag (`QUOTED_IDENTIFIER ON`) because Identity creates filtered unique indexes on `NormalizedUserName` and `NormalizedName`.
- User secrets only load in the Development environment. Other environments must set `ConnectionStrings__DefaultConnection` (for example as an environment variable).
- Earlier changelog entries mention MySQL; they describe the project as it was at the time and were left unchanged.

## 2026-09-27 — Registration and login

Commit: `257664c`

### Added
- `HealthTrack/Models/ViewModels/RegisterViewModel.cs`:
  - Required: `FullName` (2–100 characters), `Email` (valid email, max 256), `Password` (8–100 characters), `ConfirmPassword` (must match `Password`), `Role` (defaults to Client).
  - Optional: `DateOfBirth` (`DateOnly`), `Gender` (max 20 characters).
- `HealthTrack/Models/ViewModels/LoginViewModel.cs` — `Email`, `Password`, `RememberMe`.
- `HealthTrack/Controllers/AccountController.cs`:
  - `Register` (GET): shows the form; redirects to Home if already signed in.
  - `Register` (POST, anti-forgery token required):
    - `Role` must be `Client` or `Trainer`; Admin can't be chosen, even by editing the form.
    - `Gender` must be one of Male, Female, Other, Prefer not to say.
    - `DateOfBirth` can't be in the future or before 1900.
    - Creates the user (email is also the username; name and email are trimmed), assigns the role, logs the registration, signs the user in, shows a welcome message and redirects to Home.
    - Identity errors appear on the form. The "username already taken" error is hidden because it repeats the "email already taken" one.
  - `Login` (GET): shows the form and keeps `returnUrl`; redirects to Home if already signed in.
  - `Login` (POST, anti-forgery token required):
    - Signs in with `lockoutOnFailure: true` and honours "Remember me".
    - Follows `returnUrl` only when it is a local URL, which blocks open redirects; otherwise goes to Home.
    - Shows "Invalid email or password." for wrong credentials, or a 15-minute lockout message (also logged as a warning).
  - `Logout` (POST, signed-in users only, anti-forgery token required): signs out, shows "You have been signed out." and redirects to Home.
  - `AccessDenied` (GET).
  - `RegisterableRoles` and `Genders` lists, shared with the views.
- `HealthTrack/Views/Account/Login.cshtml` — centred sign-in card with floating-label email and password fields, "Remember me", an error summary and a link to register.
- `HealthTrack/Views/Account/Register.cshtml` — wider card with:
  - Client/Trainer choice shown as two selectable tiles.
  - Full name and email fields, then date of birth + gender and password + confirm password side by side (stacked on phones).
  - Password rules hint and a link to sign in.
- `HealthTrack/Views/Account/AccessDenied.cshtml` — themed "Access denied" page with a "Back to home" button.
- Both forms use the existing jQuery validation scripts, so errors appear before the form is submitted as well as after.

### Changed
- `HealthTrack/Data/ApplicationDbContext.cs` — `ConfigureConventions` now stores every `DateOnly` property through a `DateOnly`↔`DateTime` converter, keeping the column type `date`. Found in testing: `MySql.EntityFrameworkCore` threw `InvalidCastException: Unable to cast object of type 'System.DateTime' to type 'System.DateOnly'` when loading any user with a date of birth, which broke login for that user. No migration was needed because the schema is unchanged.
- `HealthTrack/Views/Shared/_Layout.cshtml` — shows `TempData["Success"]` as a dismissible green alert above the page content.
- `HealthTrack/Views/Home/Index.cshtml` — signed-in users see "Welcome back, {FullName}." instead of the Get started / Sign in buttons.
- `HealthTrack/wwwroot/css/site.css`:
  - New classes: `.ht-auth-card` (440px wide, orange top border), `.ht-auth-card-wide` (620px) and `.ht-role-option` (selectable tile with an orange border and tint when chosen, plus a focus ring).
  - Invalid fields get a red border (`.input-validation-error`), and the empty error summary is hidden (`.validation-summary-valid`).
  - Removed the template's floating-label placeholder rules, which made placeholders overlap the labels.
- `README.md` — ticked off "Registration and login".

### Environment / verification
- Build: succeeded with 0 warnings and 0 errors.
- Ran the app in Development on port 5299 and scripted the flows with curl and a cookie jar (`authtest.sh` in the session scratchpad, not in the repo). All passed after the fixes above:
  - The Login, Register and AccessDenied pages load (200).
  - Mismatched passwords → "Passwords do not match."
  - Registering with `Role=Admin` → rejected ("Please choose a valid account type.").
  - Weak password `password` → Identity's "digit" and "uppercase" errors.
  - Future date of birth plus an invalid gender → both errors shown.
  - Registration POST without an anti-forgery token → 400.
  - Valid Trainer registration → 302 to Home. The welcome message and "Welcome back, Test Trainer" both appeared.
  - Opening Login while signed in → 302 to Home.
  - Duplicate email → a single "Email '…' is already taken." error.
  - Logout → 302 to Home.
  - Wrong password → "Invalid email or password."
  - `returnUrl=https://evil.com` → 302 to Home, so the open redirect is blocked.
  - `returnUrl=/Home/Privacy` → 302 to `/Home/Privacy`.
  - Seeded admin login → 302 to Home.
  - 5 wrong passwords, then the correct one → lockout message.
  - MySQL: users stored with the correct role, date of birth and gender; passwords are hashed with the Identity v3 format (`AQAAAA…`); `LockoutEnd` is set on the locked account.
- Checked headless Chrome screenshots of Login and Register at 1200px and Register at 520px.
- Cleanup: deleted the four test accounts (`test…@example.com`, `lock…@example.com`) from `AspNetUsers`; only `admin@healthtrack.local` remains.
- Your `dotnet watch` on port 5182 was left untouched.

### Notes
- Admin accounts can't be registered. The only admin is the seeded one; admin-managed user creation can come with the admin dashboard.
- Email confirmation isn't enabled (`RequireConfirmedAccount` is off) because no email sender is configured.
- After sign-in everyone lands on Home. Role-based dashboards are the next checklist item.
- The navbar shows the signed-in user's username, which is their email address.

## 2026-09-27 — Orange & white theme / layout

Commit: `e9b28e6`

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
