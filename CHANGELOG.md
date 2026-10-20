# Changelog

A log of every task completed on HealthTrack, newest first.

## 2026-10-04 — Appointment system

Commit: not committed yet

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
