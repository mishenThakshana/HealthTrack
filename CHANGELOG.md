# Changelog

A log of every task completed on HealthTrack, newest first.

## 2026-10-23 — "My profile" card removed from the trainer dashboard

Commit: not committed yet

### Removed
- **Trainer dashboard:** the "My profile" card (avatar, star rating, email, age, gender, trainer details, member since, and the Edit / Public profile links) under Appointment requests. The right column now holds only Appointment requests. Trainers can still edit their details in My account and see their public profile from the Trainers page.
- **`Views/Dashboard/_ProfileCard.cshtml`**, which only the trainer dashboard used.
- **Data loaded only for the card:** `DashboardController.RatingOfAsync` (two review queries per page load), the `TrainerProfile` + `Qualifications` load in `DashboardController.Trainer`, the `rating` parameter of `ToProfile`, and `MemberDashboardViewModel.Rating`. The greeting ("Hi, <first name>") still comes from `Model.Profile`.

### Verified
- Built with no warnings or errors.

## 2026-10-23 — Custom date ranges for Reports, Progress and their exports

Commit: not committed yet

### Added
- **`DateRange` (`Helpers/Periods.cs`):** a period's `From`/`To`, `IsCustom`, `Days`, `Contains`, `Label` ("Last 30 days" or "1 Oct 2026 – 20 Oct 2026"), `FileSuffix` for export names ("30d" or "2026-10-01-to-2026-10-20") and `RouteDays`/`RouteFrom`/`RouteTo` for links that keep the same range.
- **`Periods.Resolve(days, from, to, defaultDays, out error)`:** uses `from`/`to` when both are given and valid, otherwise the preset. The rules: both dates are needed, the start must be on or before the end, the end can't be in the future, and the range can be at most 2 years (`MaxCustomDays` = 731). `Periods.Days/Weeks/Buckets` now take a `DateRange`, so charts end at the range's end date, not today. Week buckets are clipped to the range, and labels include the year for ranges over a year.
- **`Views/Shared/_RangePicker.cshtml`** (+ `RangePicker` record), used on Reports and Progress: the preset buttons, a **custom range** form (two date inputs capped at today, and **Apply**, highlighted when a custom range is active), and the **Excel** / **PDF** buttons, which export exactly the range on screen. `.ht-range-custom` CSS keeps the date inputs compact.

### Changed
- **`ReportsController.Index/Export`** and **`ProgressController.Index/Export`** accept `from` and `to` alongside `days`, and the builders query and chart `range.From`..`range.To`. With invalid custom dates, the pages show a warning ("The end date can't be in the future. Showing last 90 days instead.") and fall back to the preset. Export links with invalid dates return **400** with the reason rather than silently exporting a different period.
- **Exports:** the subtitle shows the exact dates ("1 Sep 2026 – 30 Sep 2026 (30 days)"), and the file name includes the range. The progress export's **Trainer feedback** now only lists feedback given within the period (before, it listed the latest 20 regardless of the period).
- The custom range is kept when a trainer adds or deletes feedback, and when opening a client's progress from the Reports table.
- README: Progress and Reports mention custom date ranges.

### Verified
- Built with no warnings. Ran on port 5299 and scripted 17 checks, all passing:
  - The custom Progress page shows its label, prefilled dates and export links with the dates.
  - The custom Excel export contains only the workout inside the range and has the dated file name. The custom PDF renders (checked visually).
  - Reversed dates, a future end date, ranges over 2 years and a missing date each show their warning. An invalid export returns 400. Presets still work.
  - Custom Reports page and Excel export work, and the client link keeps the range.
  - A trainer can export a custom range for their client. Adding feedback redirects with the range kept.
- Deleted the test client. The test trainer from the previous task was reused.

## 2026-10-23 — Health professional role removed; Excel and PDF report exports for trainers, admins and clients; JSON data download removed

Commit: not committed yet

### Why the role was removed
- The coursework brief (`AD CW 02.docx`) defines three account types: "Users (clients), Trainers, and Administrators", and only **trainers** approve, update or cancel appointments. Health professionals appear only in the case-study intro and in "Clients can schedule consultations with trainers or healthcare professionals", with no role, dashboard or workflow of their own. The role added a second client link (`ProfessionalId`), a second access path to client health data and a separate dashboard, so it was removed. Health-focused consultations are booked with trainers; a **Nutrition and diet** specialization was added for that (Injury recovery already existed).

### Removed
- **Role and data model:** `Roles.Professional`, `Roles.Providers` and `Roles.DisplayName`; `AppointmentManagers` is now Trainer + Admin and `Roles.All` is Client, Trainer, Admin. Deleted `Models/ProfessionalProfile.cs`, `ProfessionalProfileFormViewModel`, `ApplicationUser.ProfessionalId`/`Professional`/`ProfessionalProfile`, the `ProfessionalProfiles` DbSet and its relationships, `ProfileOptions.Professions`, and the `ProfessionalChosen` notification type.
- **Migration `20261023114325_RemoveHealthProfessionalRole`:** before dropping anything, any existing health professional becomes a trainer (a `TrainerProfiles` row with a matching specialization: Dietitian → Nutrition and diet, Physiotherapist → Injury recovery, others → General fitness; a qualification "Profession (reg. number)"; role switched to Trainer), so their appointments and reviews stay valid. It then removes the Professional role, deletes `ProfessionalChosen` notifications, and drops the `ProfessionalProfiles` table and the `AspNetUsers.ProfessionalId` column, index and foreign key. The dev database had no professional accounts.
- **Pages and code paths:** the professional dashboard (`DashboardController.Professional`, `Views/Dashboard/Professional.cshtml`, `ProfessionalDashboardViewModel`, `ChosenByClient`); the registration and My account "About your practice" sections; the professional branch in `RoleProfiles` (validate/apply/remove/load), `AccountController.Register`/`UpdateDetails`/`BuildManageAsync`, `UsersController` (role change unlinking, details loading), `ClientAccess` (professionals seeing clients who chose them or had appointments), `ProfilePhotosController.CanSeeAsync`, `AppointmentsController` (provider list group, includes), `DbSeeder` (profession fields) and `NotificationService`.
- **Admin dashboard:** the "Professionals" tile (the remaining five tiles now share the row).
- **JSON data download:** `AccountController.DownloadData` and the "Download my data" button. My account → Your data now links clients to **Export my report** (Progress page). The privacy policy's "Get a copy" point says the same.

### Changed
- **Find experts → Find a trainer** (`/Providers`, menu item and banner): trainers only. No Trainers/Health professionals tabs, a single "Your trainer" card, and search/filter by specialization or qualification. `Leave` no longer takes a role. `ProviderCard.Workplace`, `ProvidersPageViewModel.ShowProfessionals`/`MyProfessional`/`MyChoiceId`/`Noun` and `ProviderProfileViewModel.IsTrainer`/`Noun` were removed. The banner reads "You haven't chosen a trainer yet."
- **Progress:** only clients (their own) and trainers (their clients) can open it; feedback is trainer-only; the non-trainer breadcrumb was removed.
- **Text:** registration (Client and Trainer side by side), admin Users pages, Appointments page, booking field label ("Trainer"), home page feature card, privacy policy, My account picture hint, and comments now refer to trainers only.

### Added — report exports (Excel and PDF)
- **Packages:** `ClosedXML` 0.105.1 (Excel) and `QuestPDF` 2026.9.1 (PDF). `Program.cs` sets `QuestPDF.Settings.License = LicenseType.Community`.
- **`Services/ReportExport.cs`:** `ExportReport` (title, subtitle, summary label/value pairs, tables) and `ExportTable` records, with `ToExcel`, `ToPdf` and `ToFile(report, format, fileName)`.
  - **Excel:** a Summary sheet, then one sheet per table with an orange header row, frozen header, auto-filter and fitted column widths. Numbers and dates keep their type, so they can be sorted and summed. Text is always written as text, so a value like `=HYPERLINK(...)` is never treated as a formula (checked: the workbook has no formula cells).
  - **PDF:** A4 landscape, orange title, summary table, one section per table with a header row that repeats across pages. Columns are sized by their longest text. Empty tables say "No entries in this period." The footer reads "HealthTrack · page X of Y".
- **Reports (`ReportsController.Export(days, format)`, trainers and admins):** replaces the CSV export. It contains a summary (clients, active clients, active days per week, entries, completed appointments) plus Client performance (with a Trainer column for admins), Engagement over time, Popular plan exercises, Most logged activities and Appointments by status. **Excel** and **PDF** buttons replace the CSV button. The old CSV helper was removed.
- **Client progress report (`ProgressController.Export(id, days, format)`):** a summary (current weight, weight change, BMI, workouts, minutes, calories burned, average calories eaten, calorie target, active days) plus every Measurement (with BMI from the latest known height), Workout and Meal in the period, Top activities and Trainer feedback. Clients export their own; trainers can export their clients' (others get 404). **Excel** and **PDF** buttons sit next to the period picker on the Progress page. Every export is logged.
- An unknown `format` returns 404.

### Docs
- README: requirements note on how "healthcare professionals" is covered, tech stack (ClosedXML, QuestPDF), registration/profile/picture sections, **Find a trainer** section, new **Logging from your plans** section (previous task), ratings, appointments, progress/reports exports, security (Excel formula safety, export instead of JSON), the "Who can do what" table (Health professional row removed) and the progress checklist.

### Verified
- Built with no warnings. Ran on port 5299 (the migration applied cleanly) and scripted an end-to-end test (23 checks, all passing after fixing the test's own sheet-name regex):
  - Register offers only Client and Trainer, and the admin's role list has no professional.
  - Client Excel/PDF progress exports download with the right sheets, and the formula text stays plain text.
  - Trainers can export their own client's PDF but get 404 for another client. A client gets refused on the trainer report.
  - Trainer and admin report Excel/PDF download with the right sheets.
  - `format=json` and `/Account/DownloadData` give 404, and My account shows the export link.
  - Find a trainer lists trainers only (even with `?show=professionals`), and the booking list has no professionals group.
  - All three dashboards render, the admin dashboard has no Professionals tile, and `/Dashboard/Professional` is 404.
  - I opened the generated PDFs and checked the layout. That's why I widened the email column and set a minimum column width.
- **Cleanup:** deleted the two test client accounts. The test trainer `exporttestexporttrainer755987@example.com` ("Export Trainer") is still there, as is "Plan Trainer" from the previous task, since admins can only delete client accounts.
- **Note:** if `dotnet watch` is running, restart it. New NuGet packages and a migration need a full restart; the migration applies automatically on startup.

## 2026-10-23 — One-click logging of planned workouts and meals; "Coming up" workout schedule

Commit: not committed yet

### Added
- **`Controllers/PlanSchedule.cs`** (shared helper, used by the dashboard and both log pages): `ForClientAsync(db, clientId)` returns a `PlanScheduleViewModel` with:
  - today's workout plan and its exercises for today (`Day` = today or "any day"), each flagged `Logged` when a workout log for today has the same activity name (case-insensitive);
  - today's diet plan and its meals, each flagged `Logged` when a meal log for today has the same meal type and description;
  - `UpcomingWorkouts`: the next 6 days, each with the plan in effect that day and its exercises (an empty list = rest day; days with no plan are skipped);
  - `NextWorkoutPlan` / `NextDietPlan`: the earliest active plan that starts after today.
  - "Plan in effect" now also respects `EndDate` (the old dashboard query only checked `StartDate`, so an ended plan could still show). When plans overlap, the latest start date wins, as before.
  - `LogFormFor(exercise | meal)` and `SetsAndReps(exercise)` helpers for pre-filling log forms.
- **View models (`TrackingViewModels.cs`):** `PlanScheduleViewModel`, `PlannedExercise`, `PlannedMeal`, `UpcomingWorkoutDay`; `TrackingPageViewModel` gained `Plan`.
- **`WorkoutLogsController`:**
  - `LogPlanned(exerciseId, returnUrl)` (POST): logs today's planned exercise in one click — activity = exercise name, minutes = planned minutes, notes = "3 × 10 reps" when sets/reps are set. Exercises without planned minutes redirect to the pre-filled form instead (minutes are required on a workout log). Logging something already logged today adds nothing and says "… is already logged for today."
  - `Index?exercise=<id>`: opens the log form pre-filled from the exercise so the client can adjust before saving.
  - Only exercises scheduled for the client **today** in their own active plan are accepted; anything else returns 404 (checked with another client's exercise and a future-day exercise).
- **`MealLogsController`:** same pair — `LogPlanned(mealId, returnUrl)` copies meal type, description and calories into today's log; `Index?meal=<id>` pre-fills the form.
- `returnUrl` is only followed when local (`Url.IsLocalUrl`); otherwise the action returns to its log page.
- **Partials:** `_TodaysWorkout.cshtml`, `_TodaysMeals.cshtml`, `_UpcomingWorkouts.cshtml`. Each planned item shows a **Log** button and a pencil (**Adjust before logging**), or a green **Logged** badge once done; exercises without minutes show **Log…** which opens the pre-filled form. Upcoming shows a date chip per day with the exercise names or "Rest day", the plan title when it differs from today's plan, and a "New workout plan “…” starts Tue 27 Oct." note. The meals panel shows a matching note for a new diet plan.

### Changed
- **Client dashboard:** "Today's workout" and "Diet schedule" (renamed **Today's meals**) now use the new partials, so clients can log straight from the dashboard and are sent back there. A **Coming up** panel sits at the top of the right column when the client has a current or future workout plan. `ClientDashboardViewModel.WorkoutPlan`/`DietPlan`/`TodaysExercises` were replaced by `Plan`; the "Calories today" tile reads the target from `Plan.DietPlan`.
- **Workout log page:** "Today's workout" above the form and "Coming up" below it (only when the client has a plan).
- **Meal log page:** "Today's meals" above the form (only when the client has a current or upcoming diet plan).

### Verified
- Built with no warnings. Ran on port 5299 and scripted an end-to-end test (24 checks, all passing): admin created a trainer and client and assigned them; the trainer created a workout plan (Squats on Friday with minutes, Plank any day without minutes, Bench press on Monday), a second plan starting 27 Oct and a diet plan with two meals. As the client: correct Log/Log… buttons, Coming up shows Monday's Bench press and the new plan's Rowing, logging Squats returns to the dashboard and shows Logged, a repeat log adds nothing, an external returnUrl is ignored, Plank redirects to the pre-filled form, both meals log from the meal page, pre-fill works for meals, unknown IDs and another client's exercise give 404, and a client without plans still sees the empty states.
- **Cleanup:** deleted the two test client accounts. The test trainer `plantesttrainer754819@example.com` ("Plan Trainer") is still there, since admins can only delete client accounts.

## 2026-10-23 — Clients can rate past appointments the provider never marked as completed

Commit: not committed yet

### Fixed
- **Rating past appointments:** before, `AppointmentRules.CanReview` only allowed rating once the provider clicked **Mark as completed**, so a past appointment still marked Approved could never be rated. It now also allows **Approved appointments that have ended** (`EndsAt <= now`). The status isn't changed by rating, and the provider can still mark it completed.
  - The same rule applies to the review form on the appointment page (`_Review.cshtml`), the `Review` POST action, and the client dashboard's **Rate your appointments** panel (`DashboardController.Client` query).
- **Cleanup:** deleted the `navtest…@example.com` client account created while testing the previous task (admin → Users → Delete).

### Verified
- Built with no warnings. I couldn't test rating end to end: the dev database has no approved past appointment I could reach, and booking doesn't allow past dates.

## 2026-10-23 — Find experts moved up; client dashboard trimmed; "choose your experts" banner

Commit: not committed yet

### Changed
- **Menu (`_Layout.cshtml`):** for clients, **Find experts** is now the 2nd item, right after Dashboard (before Plans, Appointments and Track).
- **Client dashboard (`Views/Dashboard/Client.cshtml`):** removed the **My team** panel and the **profile card**. The trainer and professional are still shown on Find experts, and the header still names the trainer. The unused `ClientDashboardViewModel.Professional` property and its query in `DashboardController.Client` were removed. Trainer and professional dashboards keep their profile cards.

### Added
- **"Choose your experts" banner (`_Layout.cshtml`):** shown to clients on every page (styled like the confirm-email banner) while they have no trainer and/or no health professional. The text names what's missing ("a trainer or health professional", "a trainer" or "a health professional") and the **Find experts** button opens the matching tab (`?show=professionals` when only the professional is missing). It's hidden on the Find experts pages themselves and disappears once both are chosen.

### Verified
- Built with no warnings. Ran on port 5299, registered a new client: the menu reads Dashboard → Find experts → Plans → Appointments → Track, the banner shows on the dashboard and not on `/Providers`, and the dashboard no longer has My team or the profile card.

## 2026-10-23 — Appointment ratings and reviews; "Find experts" menu name; orange instead of Bootstrap blue

Commit: not committed yet

### Changed
- **Menu:** the client's "My team" nav item is now **Find experts** (`bi-search-heart` icon). The professional dashboard's empty-state text was updated to match. The client dashboard panel keeps the name "My team", since it shows the client's own trainer and professional.
- **Bootstrap blue replaced with the primary orange** (`site.css`): `.nav-pills` active background and link colors (the Trainers / Health professionals tabs were blue), `.text-primary-emphasis` (qualification chips were dark blue), `.dropdown-menu` active item, and the `.nav-link` focus ring. The light-blue "Completed" appointment badge (`ht-badge-completed`) is a deliberate status color and was left as is.

### Added — ratings and reviews after appointments
- **`Models/AppointmentReview.cs`:** table `AppointmentReviews` with `AppointmentId` (unique; cascade-deleted with the appointment), `ClientId`, `ProviderId` (both `NoAction`), `Rating`, `Comment` (≤1000), `CreatedAt` and `UpdatedAt`. It has check constraint `CK_AppointmentReviews_Rating` (`[Rating] BETWEEN 1 AND 5`) and index `(ProviderId, CreatedAt)`. `Comment` is added to `EncryptedColumns`, so it's encrypted at rest and covered by the legacy-row encryption pass. Migration `20261023065103_AppointmentReviews`.
- **`AppointmentRules.CanReview`:** the appointment's status is Completed.
- **`AppointmentsController`:**
  - `Details(id, editReview)` loads the review and fills `ReviewForm`.
  - `Review(id)` (clients only, own completed appointments via `Mine()`) creates or updates the review. A new review notifies the provider (`ReviewReceived`: "… rated your appointment on … 4 out of 5"). Validation errors re-show the form.
  - `DeleteReview(id, returnUrl)` lets clients delete their own reviews and admins delete any; the deletion is logged.
  - The completion notification now tells the client "Open it to leave a rating and feedback." It isn't added to the copy a provider gets when an admin completes the appointment.
  - The controller now takes an `ILogger`.
- **`Views/Appointments/_Review.cshtml`** (on the appointment page):
  - For clients: a star picker (radio buttons, keyboard-accessible, CSS-only hover/checked fill), optional feedback, Submit or Save changes, Cancel. After submitting, they see their review with Edit and Delete.
  - Providers see the review read-only or "hasn't rated this yet". Admins see it with Delete.
- **`ReviewViewModels.cs`:** `ReviewFormViewModel` (required, Range 1–5), `RatingSummary`, `ReviewItem` (with `ClientShortName` → "Omar S."), `ProviderProfileViewModel`, `ReviewPrompt`.
- **`Views/Shared/_Stars.cshtml`:** five stars (filled to the nearest half), the average, and the count (`Compact` hides the count), with screen-reader text.
- **Provider profile page `ProvidersController.Details` + `Views/Providers/Details.cshtml`:**
  - Shows the picture, role, headline, stars, experience, workplace, qualifications and client count.
  - Clients get Choose and Book buttons there; Choose now accepts a `returnUrl`.
  - A reviews panel shows the big average, a 5→1 breakdown (`<progress>` bars, since the CSP forbids inline styles) and the latest 50 reviews.
  - **Access:** clients (who can't open disabled accounts), admins (who see full names and Delete), and the provider themselves. Others get 404. The controller is now `[Authorize]`, with `Index`/`Choose`/`Leave` kept client-only.
- **Find experts page:** cards show stars and link to the profile ("Profile & reviews"); a new **Sort by name / Top rated** option (average, then review count, then name). `ProviderCard` gained `AverageRating`, `ReviewCount` and `Rating`.
- **Dashboards:**
  - **Client:** a "Rate your appointments" panel (the 3 latest completed, unrated appointments with a Rate button), shown only when there are any. My team shows each provider's stars and links to their profile.
  - **Trainer/professional:** the profile card shows their rating (linked to their reviews) and a "Public profile" link (`MemberDashboardViewModel.Rating`, `DashboardController.RatingOfAsync`).
- **Admin:** Users → Edit shows a "Profile & reviews" button for trainers and professionals.
- **Data export:** `ReviewsWritten` (clients) and `ReviewsReceived` (providers).
- **Notifications:** new `ReviewReceived` type (Updates category, `bi-star`).

### Docs
- README: new "Ratings and reviews" section; "My team" renamed to "Find experts", now covering profile pages, top-rated sorting and the "Who can do what" row.

### Verification (port 5299, Development; build 0 errors, 0 warnings)
- Migration applied on startup. Registered 2 temporary clients and 2 temporary professionals. Scripted HTTP test:
  - **Before completion:** no review panel, and `Review` → 404. Booked, approved, moved the time to the past (SQL), and completed.
  - **After completion:** the client is told to rate and the dashboard prompt links to `#review`.
  - **Validation:** a missing rating and a rating of 9 both show "Please choose a rating from 1 to 5 stars."; another client → 404.
  - **Submit:** 4 stars with a comment containing `<b>` → stored with `enc:`; the provider is notified "4 out of 5"; the HTML shows escaped. The edit form is prefilled; editing to 5 still leaves 1 row; the dashboard prompt disappears; the provider sees it read-only.
  - **Profile:** shows 5.0 and the client as "Omar S." (checked as a non-admin); "Top rated" puts the rated professional first; the provider dashboard shows "Rated 5.0 out of 5" with a profile link. The provider can open their own profile; another professional gets 404; anonymous users are redirected; a client id as a provider id → 404; admins see the full name and Delete; admins can't open `/Providers`.
  - **Admin:** the Users → Edit link works. The exports contain the reviews. Another client deleting → 404; the admin deletes via the profile page → 302 back to the profile, 0 rows left.
  - **Database:** a direct insert with rating 7 is rejected by the check constraint. Deleting the client's account removed their reviews (cascade through appointments).
- **Real browser (Playwright + Chrome, 1280px and 390px):** uploaded sample pictures through My account. Took screenshots of Find experts (orange tabs, stars, sorting), the profile page, the review form (star picker), the submitted review, the client dashboard and mobile; no console errors.
- **Cleanup:** deleted all temporary accounts with their appointments, reviews, notifications and pictures; reseeded the `Appointments` identity. No notifications went to real accounts. Stopped only the port-5299 process.

## 2026-10-23 — Profile pictures, sample photos, and clients choosing their own trainer and health professional

Commit: not committed yet

### Requirements check (CS6004ES coursework 2 brief, `AD CW 02.docx`)
- Nothing in the brief conflicts with these features. They are additions on top of the required ones:
  - **Admin dashboard "trainer assignments":** still shown. Clients' own choices use the same `TrainerId`, so they appear in the admin's assignment counts and on the Assignments page, and admins can still assign and reassign.
  - **"Trainers manage clients' routines":** unchanged. A trainer a client chooses gets the same access as one an admin assigns.
  - **Appointments with trainers or healthcare professionals:** unchanged. The browse page's Book button only preselects who to see.
  - **Notifications:** choosing, switching and removing a provider send in-app (and optional email) notifications.
  - **Security and data protection:** pictures are type-checked by content, size-limited, protected by anti-forgery tokens, and access-controlled. A client's picture is only visible to the people who work with them. Choosing a health professional is the client's own consent to share progress with them, and removing them takes that access away.
- One thing to be ready to explain in the viva: since the previous task, clients no longer enter a goal, activity level or health conditions. "Clients manage personal health data" is still covered by measurements, workout and meal logs, and Progress.

### Added — profile pictures (all roles)
- **`Models/ProfilePhoto.cs`:** table `ProfilePhotos` (`UserId` PK/FK, `Data` varbinary(max), `ContentType`), cascade-deleted with the user. Kept separate from `AspNetUsers` so loading a user doesn't load the image.
- **`ApplicationUser.PhotoUpdatedAt`:** null when there's no picture. Views use it to choose between the picture and initials, and as the `?v=` cache-buster.
- **`Helpers/ProfilePhotoRules.cs`:** 2 MB limit, and `DetectContentType` checks the first bytes for JPEG, PNG or WebP (the client-supplied Content-Type and file name are ignored).
- **`Controllers/ProfilePhotosController.cs`** (`[Authorize]`):
  - `Show(id)` returns the image with `Cache-Control: private, max-age=31536000, immutable`, or 404 if the viewer may not see it.
  - `Upload(photo)` has `[RequestSizeLimit]` and anti-forgery. It validates, upserts the row and then sets `PhotoUpdatedAt`.
  - `Remove()`.
  - **Visibility (`CanSeeAsync`):** yourself; admins; any trainer or professional's picture; a client's picture only for their trainer, their chosen professional, or providers with an appointment with them.
- **`Avatar` record** (`Models/ViewModels/AvatarViewModel.cs`) and **`Views/Shared/_Avatar.cshtml`:** renders the picture or the initials circle. Sizes `ht-avatar-md`, `-lg` and `-xl`, with CSS in `site.css`; avatars inside `main` use the soft orange tint.
- **Where it shows:** navbar account button (`_LoginPartial`, replacing the inline initials code); My account (new "Profile picture" panel with Upload/Replace and Remove, plus help text saying who can see it); dashboard profile card (with an "Add a profile picture" link); client dashboard My team; professional dashboard "Clients who chose you"; the browse page; the trainer's client table (`_ClientTable`, `ClientSummary.PhotoUpdatedAt`) and client page header; and the admin Users list (`UserRow.PhotoUpdatedAt`).
- **Data export** (`DownloadData`) now includes `HasProfilePicture` and `HealthProfessional`.

### Added — My team: clients choose a trainer and a health professional
- **`ApplicationUser.ProfessionalId`:** self-referencing FK, `NoAction`, indexed.
- **`Controllers/ProvidersController.cs`** (`[Authorize(Roles = Client)]`):
  - `Index(show, q, focus)` has Trainers and Health professionals tabs, a search (name, specialization, qualification, profession, workplace) and a specialization/profession filter validated against `ProfileOptions`. Disabled accounts are left out.
  - `Choose(id)` checks the role, not the profile row, so a trainer without a profile still counts as a trainer. It sets `TrainerId` or `ProfessionalId` and notifies the new provider and, if there was one, the replaced provider.
  - `Leave(role)` clears the choice and notifies the provider.
  - `Cards(db, users)` is a static projection that the client dashboard also uses.
- **`Views/Providers/Index.cshtml`:** "Your trainer" and "Your health professional" summary with Remove (confirm); provider cards with picture, headline, experience, workplace, qualification chips and client count; "Choose as my …" (asks to confirm when replacing someone); Book. The current choice is highlighted (`ht-provider-mine`).
- **Nav:** "My team" link for clients.
- **Client dashboard:** new "My team" panel (trainer and professional with pictures, or links to choose one). The header greeting now uses `Model.Trainer` (`ClientDashboardViewModel.TrainerName` replaced by `Trainer`/`Professional` `ProviderCard`s).
- **Professional dashboard:** "Clients who chose you" panel linking to each client's Progress page (`ProfessionalDashboardViewModel.ChosenBy`).
- **`ClientAccess.VisibleClients`:** professionals now also see clients whose `ProfessionalId` is them, which gives them Progress and feedback access.
- **Appointments:** `Index(with)` preselects a provider if the id is a trainer or professional. The booking panel has `id="book"`.
- **Notifications:** new `ProfessionalChosen` and `ClientLeft` types (Updates category; icons `bi-hospital` and `bi-person-dash`). The trainer's "New clients assigned to me" switch is now "Clients choosing or leaving me" and also shows for health professionals.
- **Admin role changes (`UsersController.Edit`):** a non-client loses both `TrainerId` and `ProfessionalId`. When a professional's role changes, clients who chose them are unlinked (there's no admin screen for that link, so blocking the change would leave admins stuck).
- Migration `20261023061634_ProfilePhotosAndChosenProfessional`.

### Added — sample pictures
- `profile-pictures/` (repository root): `trainer-1..3`, `professional-1..2`, `client-1..3`, `admin-1` (`.jpg`, 600×600, 27–97 KB), all CC0 or public domain from Wikimedia Commons (mostly Unsplash and Pexels imports). `CREDITS.md` lists the source, photographer and licence for each.
- Unsplash, Pexels and Pixabay block scripted downloads (bot check / 403), and Openverse's CC0 stock (rawpixel) was mostly historical images. Commons "Personal Trainer (…)" files turned out to be photos of a band of that name, so they weren't used.

### Docs
- README: new "Profile pictures" and "My team" sections. Updated the client registration details (no health details section any more), feedback access, the "Who can do what" table and the progress list. Also updated a stale comment in `AccountController.Register`.

### Verification (port 5299, Development; build 0 errors, 0 warnings)
- Migration applied on startup. Registered a temporary client and professional, and ran a scripted HTTP test:
  - **Upload:** PNG → 302, picture shown on My account; `Show` returns `image/png` with the long private cache header.
  - **Rejected uploads:** a text file named `.png` → "Please upload a JPG, PNG or WebP picture."; a file over 2 MB → "too big"; a request without the anti-forgery token → 400.
  - **Visibility:** the professional got 404 for the client's picture before being chosen and 200 after; the client sees the professional's picture (200); admin 200; anonymous users are redirected to login.
  - **Browse:** trainers tab lists Coach Reddy; searching "physio" finds the professional and their workplace; a search with no matches shows the empty message; the Nurse filter hides the physiotherapist. Professionals and admins can't open `/Providers`.
  - **Choose a professional:** the card is marked "Your health professional"; the professional's dashboard lists the client, `/Progress/Index/{client}` gives 200, and they get the "chose you" notification; the client's dashboard My team shows them.
  - **Book link:** preselects the provider.
  - **Choose a trainer:** the dashboard says "Your trainer is …". Choosing a non-provider id (the client's own) → 404.
  - **Remove the professional:** their access to the picture and Progress → 404, and they get the "removed you" notification. Removing the picture → `Show` 404.
  - Users, the admin dashboard and Assignments → 200. The data export contains the new fields. No errors in the server log.
- **Cleanup:** deleted the two temporary accounts (and their picture and profile by cascade) and the two test notifications sent to Coach Reddy. Stopped only the port-5299 process.
- Not checked in a real browser; layout was only checked through the HTML.

## 2026-10-23 — Remove "About your health" from clients' My account page

Commit: not committed yet

### Changed
- **`Views/Account/Manage.cshtml`:** the "About your health" panel (main goal, activity level, health conditions, and the link to Measurements) no longer shows for clients. The details panel now only appears for trainers ("About your coaching") and professionals ("About your practice").
- **`DashboardController.ToProfile`:** new `canAddDetails` parameter (default `true`). The client dashboard passes `false`, so clients no longer see the "Your profile is missing some details. Add them now" note, which linked to the removed panel. Trainers and professionals still see it.
- Not changed: `AccountController.UpdateDetails` still accepts clients, and the stored client profile (goal, activity, health conditions) is kept and still shows on the client dashboard profile card when set.

### Verification
- `dotnet build`: 0 errors.

## 2026-10-22 — Keep only Coach Reddy and Mishen Thakshana; admins manage appointments

Commit: not committed yet

### Removed (database and seed data)
- **Demo accounts deleted** from the local SQL Server database (`HealthTrack` in the `sql` container), along with all their data:
  - `trainer@healthtrack.local` (Alex Morgan), `client@healthtrack.local` (Jamie Silva), `doctor@healthtrack.local` (Dr. Sam Patel).
  - The script ran in one transaction. It first cleared `AspNetUsers.TrainerId` where it pointed at a deleted user, then deleted their `Notifications`, `Appointments`, `ProgressFeedback`, `HealthMeasurements`, `MealLogs`, `WorkoutLogs`, `DietPlans` and `WorkoutPlans` (exercises and meals cascade), then deleted the users. Role links, profiles and qualifications were removed by cascade.
  - Accounts left: `admin@healthtrack.local`, `coachreddy@gmail.com` (Coach Reddy, Trainer, with trainer profile and 2 qualifications), and `mishenthak@gmail.com` (Mishen Thakshana, Client). Their data was left alone: 1 approved appointment and 4 notifications.
  - Ran with `sqlcmd -I`. Without it, `QUOTED_IDENTIFIER` is off, so the update on `AspNetUsers` fails because of the filtered indexes.
- **`appsettings.Development.json`:** `SeedUsers` now holds only the admin, so the seeder doesn't recreate the demo accounts on the next start.
- **Docs:** the README "Seeded accounts" section lists only the admin and explains how to add more seed users. `TESTING-WALKTHROUGH.md` §2.3 has a note that the demo trainer, client and professional need to be registered first.

### Added — admins manage appointments
- `Roles.AppointmentManagers` (`Trainer,Professional,Admin`). `AppointmentsController` now allows admins at class level, and Approve, Decline, Complete and Edit (GET and POST) accept them too. Create (booking) stays client-only.
- **Controller (`AppointmentsController`):**
  - `IsAdmin`. `IsProvider` is also true for admins, so they get the manager UI and rules (for example, they can't cancel a pending request; they decline it instead).
  - `Mine()` returns every appointment for an admin. `FindOwnAsync` now also loads `Trainer`.
  - Clash checks in Approve and Edit use the appointment's `TrainerId` instead of the signed-in user's, with the message "<trainer> already has an appointment at that time" for admins.
  - Notifications: `MyNameAsync()` returns "An administrator" for admins. The new `NotifyClientAsync(appointment, type, title, whose => …)` tells the client ("your appointment"). When an admin acted, it also tells the trainer or professional ("Mishen Thakshana's appointment"). An admin cancel notifies both sides.
- **`Appointment.CancelledByAdmin`** (bit, default 0). Migration `20261022164202_AdminCancelledAppointments`. Details shows "Cancelled by: An administrator".
- **View models:** `AppointmentsPageViewModel.IsAdmin`, `AppointmentDetailsViewModel.IsAdmin`.
- **Views:**
  - `_Layout`: the Appointments nav link now shows for admins.
  - `Appointments/Index`: admin subtitle "Review and manage every client's appointments with trainers and health professionals."; no booking form.
  - `_AppointmentItem`: list rows show "Client with Trainer" for admins.
  - `Appointments/Details`: title "Client with Trainer", a "With" row (provider name, headline, email), and the client name links to Users/Edit. A note is labelled with the trainer's name instead of "you".
  - `Appointments/Edit`: subtitle "Client with Trainer" for admins.

### Verification (port 5299, Development; build 0 errors, 0 warnings)
- On startup the migration applied, and only the 3 accounts exist (nothing was re-seeded).
- Used a temporary pending appointment between Mishen and Coach Reddy, signed in as admin (curl):
  - The Appointments nav link shows. Index shows the admin subtitle and "… with Coach Reddy" rows. Details shows Approve / Decline / Change time & approve.
  - Edit to 4 Nov 14:00, 45 min, video, location `Zoom {link}` → 302. Approved. The client and the trainer got "An administrator confirmed your / Mishen Thakshana's appointment…" (checked in Mailpit; braces in user text are fine).
  - Cancel with a reason → status Cancelled, `CancelledByAdmin = 1`, Details "An administrator on 22 Oct 2026", both notified.
  - Reset to pending → Approve → Approved, both notified.
- Cleanup: deleted the test appointment and its 6 notifications and reseeded the `Appointments` identity. Stopped only the port-5299 process.

### Notes
- Restart your app (`dotnet run` / `dotnet watch`) so it applies the migration and picks up the new code.
- Admins can't book appointments on a client's behalf. Booking is still done by clients.

## 2026-10-22 — Hide the Home link for signed-in users

Commit: not committed yet

### Changed
- **Top bar:** the "Home" nav link (`Views/Shared/_Layout.cshtml`) now shows only to signed-out visitors. Signed-in users see Dashboard as the first link instead. The HealthTrack brand logo still links to the home page.

## 2026-10-21 — Registration layout: no client health step, compact provider sections, multiple qualifications

Commit: not committed yet

### Changed
- **Clients aren't asked health questions at registration.**
  - The "About your health" section is removed, along with height and weight.
  - `RegisterViewModel` no longer has `HeightCm`, `WeightKg` or `Client`, and registration no longer creates a first measurement.
  - New clients see the dashboard's "Your profile is missing some details. Add them now" prompt and add their goal, activity and health notes under My account (`UpdateDetails`, unchanged).
  - Date of birth stays, beside Gender.
- **Gender / date-of-birth row** uses `col-sm` columns, so for trainers and professionals (no date of birth) Gender takes the full row instead of half.
- **Trainer section:**
  - Specialization (`col-sm-7`) and Experience (`col-sm-5`, a floating input inside an input group with a "years" suffix) share one line.
  - Qualifications is now a list: one input per qualification, "+ Add another qualification", and × to remove (hidden while only one row is left), up to 10.
  - In the registration sections the inputs match the floating-field height; the remove buttons use the normal border colour.
- **Professional section:**
  - Profession and Experience (with "years") on one line; Registration / licence no. and Workplace (optional) on the next.
  - Shorter labels ("Registration / licence no.", "Workplace (optional)", "Experience") and messages ("Enter your registration number.", "Enter your experience.", "Enter 0 to 60 years.", "Use 3 to 50 characters."), so nothing is cramped or wraps.
  - Validation messages sit under each field instead of inside the floating wrapper.
- **My account:** the trainer and professional panels use the same 7/5 split with the "years" suffix, and trainers get the same qualification list.

### Added
- **`TrainerQualifications` table** (`Models/TrainerQualification.cs`): one-to-many from `TrainerProfiles`, cascade delete, `Name` ≤ 100.
  - `TrainerProfile.Certifications` is replaced by `Qualifications`.
  - Migration `TrainerQualifications`:
    - Creates the table, then copies each comma-separated part of the old `Certifications` text into its own row (`STRING_SPLIT` with ordinal) before dropping the column. The seeded trainer kept "Level 3 Personal Trainer" and "First Aid at Work".
    - `Down` joins the rows back into the column with `STRING_AGG`.
- **Qualifications form and rules:**
  - `TrainerProfileFormViewModel.Qualifications` (`List<string?>`).
  - `RoleProfiles.Validate`: at least 1, at most `MaxQualifications` (10), each 2–100 characters.
  - `CleanQualifications`: drops blanks and case-insensitive duplicates.
  - `ApplyAsync` replaces the list.
- **Qualification list UI:**
  - `Views/Shared/_QualificationsField.cshtml`, rendered with `<partial for="Trainer">` on registration and My account. The first input carries the browser-side "Add at least one qualification." rule.
  - `site.js` `data-repeat-list`:
    - Add clones the first row without its validation attributes.
    - Remove shifts the values up and drops the last row, so names stay `[0..n-1]` and the validated first input is kept.
- **Where qualifications appear:** profile cards and the admin's Coaching details (comma-separated), the data export (a list), and the seeder (config `"Qualifications": [ ... ]`).

### Verification (port 5299, Development; build 0 errors)
- **Migration:** applied; 2 qualification rows for the seed trainer; `Certifications` column gone.
- **Screenshots (headless Chrome) at 1280 px and 390 px:**
  - Client form shows only name, email, gender + date of birth, password.
  - Trainer section: one-line specialization/experience; qualification rows the same height as other fields.
  - Professional section aligned, with and without errors.
- **Browser flows (no JS errors):**
  - Trainer: added 3 qualifications and removed the middle one → saved "Level 3 PT, Nutrition Coach" in order.
  - My account: removed the first, added "First Aid" → saved; removing all → "Add at least one qualification."
  - Professional registration with an empty workplace → card without workplace.
  - Client: registered with no health fields → missing-details prompt; added goal/activity → card updated, prompt gone.
- **Found and fixed during testing:** client registration failed because the hidden trainer/professional sections' required rules weren't discarded for clients. `RoleProfiles.Validate` now always runs at registration.
- **curl:**
  - No qualifications → error. 11 → "Add up to 10". A 1-character entry → length error.
  - "CPR", "", "cpr", "Pilates Mat" → saved as ["CPR", "Pilates Mat"] (checked in the export).
  - Admin user page for the seed trainer shows the migrated qualifications.
- **Cleanup:**
  - Recorded a baseline first (4 users, sign-in times NULL, no notifications, empty Mailpit).
  - Deleted the 4 test accounts; their qualification rows were removed by cascade.
  - Reset sign-in times set after the test start (14:05:17 UTC), deleted the 4 test emails from Mailpit, stopped my app instance and headless Chrome, and emptied the scratchpad.
  - The database matches the baseline, plus the seed trainer's 2 migrated qualifications.
  - The user's `dotnet watch` on 5182 wasn't touched.

## 2026-10-20 — Gender rules and role-specific registration (one-to-one profile tables)

Commit: not committed yet

### Gender
- Options are now **Male / Female** only (`AccountController.Genders`); "Other" and "Prefer not to say" are rejected with "Please choose a valid option."
- **Required for everyone except admins:**
  - `[Required(ErrorMessage = "Please select your gender.")]` on `ProfileFormViewModel` (My account) and `UserFormViewModel` (admin Users forms), as well as on registration.
  - "Not set" options renamed "Select".
- **Admins have no gender:**
  - My account hides the field for admins, and `UpdateProfile` drops the rule and stores NULL.
  - On Users → Create/Edit the gender field is wrapped in `data-hide-for-role="Admin"`. It hides and disables while the role is Admin, and `UsersController.ValidateProfile` drops the rule and stores NULL for admin accounts.
- **Seed data:**
  - The admin has no gender (removed from `appsettings.Development.json`, and set to NULL in the current database).
  - The doctor's gender changed from "Prefer not to say" to "Male", in config and in the database. This was an arbitrary choice for seed data; change it if you prefer.

### Role-specific registration
- **New tables, one-to-one with `AspNetUsers`** (migration `RoleProfiles`): `ClientProfiles`, `TrainerProfiles` and `ProfessionalProfiles`.
  - Each has an int `Id`, plus a `UserId` FK with a **unique index** and **cascade delete**. The int `Id` is kept so the existing legacy-encryption pass works.
  - Models `ClientProfile`, `TrainerProfile`, `ProfessionalProfile` and navigations on `ApplicationUser`.
  - `ClientProfiles.HealthConditions` is added to `EncryptedColumns` (🔒).
- **What each role is asked:**
  - **Client:** date of birth (now required), height and weight (saved as the first `HealthMeasurement`, note "Starting measurement from registration"), main goal, activity level, and injuries/conditions/allergies (optional, ≤ 500, encrypted).
  - **Trainer:** specialization, years of experience (0–60), qualifications or certifications (2–200).
  - **Health professional:** profession, registration or licence number (3–50), years of experience (0–60), clinic or workplace (optional, ≤ 100).
  - Fixed option lists are in `Helpers/ProfileOptions.cs`.
- **How the form works:**
  - `Register.cshtml` shows one orange-tinted section (`.ht-role-section`) per account type. Date of birth only appears for clients.
  - `site.js` now handles `data-show-for-role` / `data-hide-for-role` for both role radios and role selects. Hidden sections are disabled, so they aren't validated or posted.
  - On the server, `Controllers/RoleProfiles.cs` removes the other roles' fields from ModelState, checks list values, and saves (`ApplyAsync`).
- **My account:** a new **About your health / coaching / practice** panel posts to `AccountController.UpdateDetails` (Client/Trainer/Professional). It creates the row if it's missing. Clients must keep a date of birth.
- **Where the details appear:**
  - **Dashboard profile cards** (`ProfileOptions.Details`). They show "Your profile is missing some details. Add them now" when an account has none, for example accounts created by an admin.
  - **Booking list:** "Name — Specialization/Profession".
  - **Appointment page:** the provider's summary, shown to the client.
  - **Client's health details** for the trainer and providers: new `Shared/_ClientHealthProfile.cshtml` panel on `Clients/Details` and on Progress (provider view).
  - **Admin user page:** a read-only Coaching/Practice details panel. Client health details aren't shown to admins.
  - **Data export:** `HealthDetails` / `CoachingDetails` / `PracticeDetails`.
- **Admin role change:** `RoleProfiles.RemoveOtherRolesAsync` deletes the details kept for the old role.
- **Seeder:** `SeedUser` has optional profile fields, and `SeedRoleProfileAsync` adds them to seed users that don't have them yet. The new config values:
  - Jamie: "Lose weight" / "Lightly active" / "Mild knee pain when running".
  - Alex: "Strength and muscle" / 6 years / "Level 3 Personal Trainer, First Aid at Work".
  - Dr. Patel: "General practitioner (doctor)" / MED-204518 / City Health Clinic / 12 years.
- **Privacy page:** what we keep (health details, work details), who sees them (clients see providers' specialization/profession, admins check providers' details but not client health details), and health notes added to the encrypted list.
- **README:** a new "Registration and role details" section, the encryption list, seed notes and the progress checklist.
- **`TESTING-WALKTHROUGH.md`:**
  - Seed table and supporting files.
  - New tables, and the **1 : 1 relationships** in 4.2.
  - Encrypted column list.
  - Tests A-03/A-04/A-05 (role sections, new validation, tamper tests), B-03/B-04/B-05, C-01/C-03/C-13, D-01/D-07, E-01/E-06, new H-02b, J-02/J-04.
  - Access matrix and one new observation (the browser's generic message for experience over 60).

### Verification (port 5299, Development)
- Build: 0 errors. The migration applied on startup. The seeder created all three seed profiles; Jamie's health notes are stored as `enc:CfDJ8…`.
- **Headless Chrome (DevTools protocol), no JS errors:**
  - Registration shows only the chosen role's section. An empty submit gives only that role's errors.
  - **Trainer, professional and client registrations:** each succeeds, and the profile card shows its details.
  - The new client's BMI tile shows "27.7 Overweight" from the registration measurement.
  - The booking list shows "Alex Morgan — Strength and muscle", "Dr. Sam Patel — General practitioner (doctor)" and the new providers.
  - **My account details:** pre-filled; clearing the goal shows an error; saving shows "Your details have been saved." and jumps to `#details`. Clearing a client's date of birth shows an error.
  - **Trainer:** sees Jamie's "About this client" panel on Clients and Progress. Trainer experience 99 → range error.
  - **Admin:**
    - Sees the doctor's practice details, and no health panel on a client.
    - On the admin2 edit page the gender field is hidden.
    - An admin-created trainer sees the missing-details prompt, and it's replaced by the details once added.
  - **Earlier gender run:**
    - Options are Male/Female.
    - The admin's My account has no gender field and saves.
    - Users → Create hides gender when the role is switched to Admin and creates the admin without one. A client without a gender is rejected.
    - Users → Edit with gender cleared is rejected.
    - A client clearing their gender is rejected.
    - Forged "Other"/"Prefer not to say" are rejected.
- **curl:**
  - A forged specialization is rejected.
  - A client post without the role fields gets all five client errors.
  - A trainer post with extra client fields creates no client profile or measurement, and date of birth stays NULL.
  - The export shows the decrypted `HealthDetails` and the starting measurement.
  - Client self-deletion removed the user, profile and measurement (cascade).
  - Admin role change Professional → Trainer deleted the `ProfessionalProfiles` row.
- **Cleanup:** deleted every non-seed account created during testing. The database now has the 4 seed accounts, 3 role profiles and no measurements.
- **Follow-up cleanup** (after the user asked whether test data was cleaned):
  - Deleted the 8 Mailpit emails sent to test addresses.
  - Reset `LastLoginAt` to NULL on the admin, Jamie and Alex. Only values from the test window (12:40–13:05 UTC) were reset, since my test sign-ins had set them.
  - Emptied the scratchpad (scripts, headless-Chrome profiles, cookie jars with session cookies, logs).
  - Checked all 20 tables: only seed rows remain.
  - Left the user's own app running on port 5182.

## 2026-10-20 — Registration: privacy checkbox fix and required gender

Commit: not committed yet

### Fixed
- **The privacy checkbox always failed in the browser, even when ticked.**
  - Cause: `[Range(typeof(bool), "true", "true")]` rendered `data-val-range-min="True"` / `data-val-range-max="True"`. jQuery Validate's range rule compares numbers, so `"true"` was never in range.
  - Fix: new `Helpers/MustBeTrueAttribute.cs`. It's a `ValidationAttribute` (server: value must be `true`) and an `IClientModelValidator` that emits `data-val-mustbetrue`.
  - New `wwwroot/js/validation.js` registers the matching `mustbetrue` method and adapter (the checkbox must be checked). This is needed because the unobtrusive `required` adapter deliberately skips checkboxes.
  - `_ValidationScriptsPartial.cshtml` now loads `validation.js` after the unobtrusive script. It's harmless on forms without the attribute.

### Changed
- `RegisterViewModel.Gender` is now `[Required(ErrorMessage = "Please select your gender.")]`. The "Select" option (empty value) shows that message in the browser and on the server. The existing server check that the value is one of Male/Female/Other/Prefer not to say still applies. Gender stays optional for admin-created users and on My account.
- `TESTING-WALKTHROUGH.md`:
  - A-03 has rows for missing gender and for a ticked privacy box, and the Behind the scenes note names the new files.
  - A-04 says to choose a gender.
  - The `sqlcmd` command now includes `-I` (QUOTED_IDENTIFIER). Without it, DELETEs on tables with filtered indexes (`Notifications`, `AspNetUsers`) fail with error 1934.

### Verification
- `dotnet build`: 0 errors. Ran on port 5299 (Development).
- **Rendered HTML:**
  - The checkbox now has `data-val-required` and `data-val-mustbetrue`, with no range attributes.
  - The gender `<select>` has `data-val-required="Please select your gender."`.
- **Server (curl POSTs with an antiforgery token):**

  | Case | Result |
  |------|--------|
  | No gender | "Please select your gender." |
  | Privacy unticked | Privacy error |
  | Forged gender `Robot` | "Please choose a valid option." |
  | Valid | 302 → /Dashboard |

- **Browser (headless Chrome over the DevTools protocol):**
  - An empty submit shows all six errors, including gender and privacy.
  - Privacy ticked without a gender shows only the gender error and stays on the page.
  - Choosing a gender then submits and shows the client dashboard welcome.
  - No JavaScript errors or exceptions.
- Removed the three test accounts created during verification (`regtest4@`, `browsertest@`, `browsertest2@example.com`) with sqlcmd. Only the four seed accounts remain.

## 2026-10-20 — Privacy policy in plain language, encryption section and contact email

Commit: not committed yet

### Changed
- `Views/Home/Privacy.cshtml` rewritten in everyday words. Technical terms are gone: "salted hash", "HTTPS", "rate-limited", "application log", "portability", "erasure". Sections: What we keep, Why we keep it, Who can see it, **Encryption: how we lock your information**, Other ways we keep it safe, How long we keep it, Your choices, Cookies, **Contact us**.
- **New encryption section:**
  - What encryption is (scrambled text plus a secret key, kept separate from the data).
  - Exactly what's encrypted: workout and measurement notes, meals, appointment reasons, provider notes, feedback and notification messages.
  - What that protects against, and when the information is unlocked.
  - Passwords are kept only as a one-way code.
  - Numbers such as weight and calories aren't encrypted, so charts can be drawn.
- **Other corrections:**
  - "Every page needs you to be signed in" is now "you need to sign in to see any health information" (Home and Privacy are public).
  - The admin line now says admins see activity numbers in reports, which matches the Reports page.
  - The page now mentions the 15-minute lockout after 5 wrong passwords and the 8-hour sign-out.
- **Contact email:** a new `ContactEmail` setting in `appsettings.json` (`privacy@healthtrack.local`). The page reads it through an injected `IConfiguration` and shows a `mailto:` link. If the setting is empty, the page falls back to "Contact your HealthTrack administrator".

### Verification
- `dotnet build`: 0 errors.
- Ran on port 5299 (Development), fetched `/Home/Privacy`, and confirmed all headings render and the link is `mailto:privacy@healthtrack.local`. Stopped that process afterwards.
- That run also recreated and seeded the database dropped in the previous task (roles and the 4 seed accounts).

## 2026-10-20 — Database reset

Commit: none (no file changes besides this log)

- Confirmed the app wasn't running (nothing listening on 5182 or 5299), so nothing was connected to the database.
- Ran `dotnet ef database drop --force --project HealthTrack` → "Successfully dropped database 'HealthTrack'." It used the connection string from user secrets.
- On the next run, `DbSeeder` recreates the database from all 6 migrations, creates the 4 roles, and seeds the 4 Development accounts. It also assigns the seed client to the seed trainer.
- Not touched: the Data Protection keys in `~/.healthtrack/keys` (still needed for new encrypted data and cookies) and old emails in Mailpit.

## 2026-10-20 — Full functional testing walkthrough

Commit: not committed yet

### Added
- `TESTING-WALKTHROUGH.md` (repo root, ~1,260 lines): a step-by-step test script covering every feature, in the order the system is used.
  - **Setup:** prerequisites, startup seeding, seeded accounts, and helper commands (sqlcmd in the `sql` container, `curl -I` for headers, `Reminders__*` overrides to trigger reminders straight away).
  - **Reference sections:** role/navigation map; every controller with the views, helpers and tables it uses; all tables and columns; each relationship with cardinality, FK and delete behaviour; the 7 encrypted columns; the security controls.
  - **Relationships:** there are no true 1:1 relationships. Trainer→clients is a 1:many self-reference on `AspNetUsers.TrainerId`. User↔role is many:many in the schema but effectively 1:1 because the app gives each user one role. Plan→exercises/meals cascade, logs/measurements/notifications cascade from the user, and plans/appointments/feedback are No action. The document explains why `AccountDeletion` deletes rows in a fixed order.
  - **Test parts A–J (≈95 test IDs):** public pages and auth; Admin; Trainer; Client; Health professional; the appointment lifecycle (with a state diagram and an action table); notifications, emails and reminders; My account; security (IDOR, antiforgery, rate limit, CSP, XSS, encryption-at-rest SQL checks, legacy-row encryption, cookies, production safety, audit log lines); privacy (export, erasure with SQL checks, retention).
  - Each test lists its steps, the expected messages (taken from the views and controllers), and a "Behind the scenes" note: controller/action, views, tables read and written, encryption and security.
  - An access-control matrix (URL × role), 7 observations for testers to confirm (for example, admin email changes don't trigger re-confirmation, and an unassigned trainer keeps edit access to the plans they wrote), and a Pass/Fail sign-off checklist.

### Notes
- Documentation only. No code, configuration or database changes.
- Written from the source at commit `351ee74`. UI labels quoted in the document were checked against the Razor views. The test steps themselves weren't run as part of this task.

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
