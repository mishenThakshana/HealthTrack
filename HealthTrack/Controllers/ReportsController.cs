using HealthTrack.Data;
using HealthTrack.Helpers;
using HealthTrack.Models;
using HealthTrack.Models.ViewModels;
using HealthTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers;

[Authorize(Roles = Roles.Trainer + "," + Roles.Admin)]
public class ReportsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, ILogger<ReportsController> logger) : Controller
{
    private string UserId => userManager.GetUserId(User)!;
    private bool IsAdmin => User.IsInRole(Roles.Admin);

    private const int DefaultDays = 30;

    // A preset period (days) or a custom one (from and to, both needed).
    public async Task<IActionResult> Index(int days = DefaultDays, DateOnly? from = null, DateOnly? to = null)
    {
        var range = Periods.Resolve(days, from, to, DefaultDays, out var rangeError);
        return View(await BuildAsync(range, rangeError));
    }

    // Downloads the report shown on the page as an Excel workbook or a PDF.
    public async Task<IActionResult> Export(int days = DefaultDays, DateOnly? from = null, DateOnly? to = null, string format = ReportExport.Excel)
    {
        if (!ReportExport.IsFormat(format))
            return NotFound();

        // Invalid custom dates can't be fixed from a download link, so they're refused rather than silently replaced.
        var range = Periods.Resolve(days, from, to, DefaultDays, out var rangeError);
        if (rangeError is not null)
            return BadRequest(rangeError);

        var report = await BuildAsync(range);

        string[] trainerHeader = IsAdmin ? ["Trainer"] : [];
        var clients = new ExportTable("Client performance",
            ["Client", "Email", .. trainerHeader, "Workouts", "Workout minutes", "Calories burned",
             "Avg calories eaten per logged day", "Measurements", "Weight change (kg)", "Active days", "Last activity", "Engagement"],
            report.Clients.Select(c => (IReadOnlyList<object?>)
            [
                c.FullName, c.Email, .. IsAdmin ? new object?[] { c.TrainerName ?? "Unassigned" } : [],
                c.Workouts, c.Minutes, c.CaloriesBurned, c.AverageCaloriesEaten, c.Measurements, c.WeightChange,
                c.ActiveDays, c.LastActivity, c.Engagement
            ]).ToList());

        var engagement = new ExportTable("Engagement over time",
            ["Period", .. report.Engagement.Series.Select(s => s.Label)],
            report.Engagement.Labels.Select((label, i) => (IReadOnlyList<object?>)
                [label, .. report.Engagement.Series.Select(s => (object?)s.Values[i])]).ToList());

        var export = new ExportReport(
            IsAdmin ? "HealthTrack client report (all trainers)" : "HealthTrack client report",
            $"{range.From:d MMM yyyy} – {range.To:d MMM yyyy} ({(range.IsCustom ? "" : "last ")}{range.Days} days) · generated {DateTime.Now:d MMM yyyy HH:mm}",
            [
                ("Clients", report.TotalClients),
                ("Active clients", report.ActiveClients),
                ("Average active days per client per week", report.AverageActiveDaysPerWeek),
                ("Entries logged", report.TotalEntries),
                ("Appointments completed", report.AppointmentsCompleted)
            ],
            [
                clients,
                engagement,
                new("Popular plan exercises", ["Exercise", "Plans", "Clients"],
                    report.TopExercises.Select(e => (IReadOnlyList<object?>)[e.Name, e.Plans, e.Clients]).ToList()),
                new("Most logged activities", ["Activity", "Sessions", "Minutes", "Clients"],
                    report.TopActivities.Select(a => (IReadOnlyList<object?>)[a.Activity, a.Sessions, a.Minutes, a.Clients]).ToList()),
                new("Appointments by status", ["Status", "Appointments"],
                    report.Appointments.Select(a => (IReadOnlyList<object?>)[a.Status, a.Count]).ToList())
            ]);

        logger.LogInformation("User {UserId} exported the client report as {Format} ({From} to {To}, {Rows} clients)",
            UserId, format, range.From, range.To, report.Clients.Count);
        return ReportExport.ToFile(export, format, $"healthtrack-client-report-{Dates.Today:yyyy-MM-dd}-{range.FileSuffix}");
    }

    private static string EngagementLevel(int activeDays, int days)
    {
        var perWeek = activeDays * 7m / days;
        return perWeek switch
        {
            >= 3 => "High",
            >= 1 => "Medium",
            > 0 => "Low",
            _ => "Inactive"
        };
    }

    private async Task<ReportsViewModel> BuildAsync(DateRange range, string? rangeError = null)
    {
        var (from, to, days) = (range.From, range.To, range.Days);
        var fromTime = from.ToDateTime(TimeOnly.MinValue);

        var clientQuery = IsAdmin ? db.UsersInRole(Roles.Client) : db.Users.Where(u => u.TrainerId == UserId);
        var clients = await clientQuery.OrderBy(u => u.FullName)
            .Select(u => new { u.Id, u.FullName, u.Email, TrainerName = u.Trainer != null ? u.Trainer.FullName : null })
            .ToListAsync();
        var ids = clients.Select(c => c.Id).ToList();

        var workouts = await db.WorkoutLogs.Where(l => ids.Contains(l.ClientId) && l.Date >= from && l.Date <= to)
            .Select(l => new { l.ClientId, l.Date, l.Activity, l.DurationMinutes, l.CaloriesBurned }).ToListAsync();
        var meals = await db.MealLogs.Where(l => ids.Contains(l.ClientId) && l.Date >= from && l.Date <= to)
            .Select(l => new { l.ClientId, l.Date, l.Calories }).ToListAsync();
        var measurements = await db.HealthMeasurements.Where(m => ids.Contains(m.ClientId) && m.Date >= from && m.Date <= to)
            .OrderBy(m => m.Date).ThenBy(m => m.Id)
            .Select(m => new { m.ClientId, m.Date, m.WeightKg }).ToListAsync();
        var lastActivity = await db.WorkoutLogs.Where(l => ids.Contains(l.ClientId)).Select(l => new { l.ClientId, l.Date })
            .Concat(db.MealLogs.Where(l => ids.Contains(l.ClientId)).Select(l => new { l.ClientId, l.Date }))
            .Concat(db.HealthMeasurements.Where(m => ids.Contains(m.ClientId)).Select(m => new { m.ClientId, m.Date }))
            .GroupBy(x => x.ClientId).Select(g => new { ClientId = g.Key, Last = g.Max(x => x.Date) })
            .ToDictionaryAsync(x => x.ClientId, x => x.Last);

        var activeDates = workouts.Select(w => (w.ClientId, w.Date))
            .Concat(meals.Select(m => (m.ClientId, m.Date)))
            .Concat(measurements.Select(m => (m.ClientId, m.Date)))
            .Distinct().ToList();

        var rows = clients.Select(c =>
        {
            var w = workouts.Where(x => x.ClientId == c.Id).ToList();
            var mealDays = meals.Where(x => x.ClientId == c.Id && x.Calories is not null)
                .GroupBy(x => x.Date).Select(g => g.Sum(x => x.Calories!.Value)).Where(v => v > 0).ToList();
            var weights = measurements.Where(x => x.ClientId == c.Id).ToList();
            var active = activeDates.Count(x => x.ClientId == c.Id);
            return new ClientReportRow(
                c.Id, c.FullName, c.Email ?? string.Empty, c.TrainerName,
                w.Count, w.Sum(x => x.DurationMinutes), w.Sum(x => x.CaloriesBurned ?? 0),
                mealDays.Count == 0 ? null : (int)Math.Round(mealDays.Average()),
                weights.Count, weights.Count > 1 ? weights[^1].WeightKg - weights[0].WeightKg : null,
                active, lastActivity.TryGetValue(c.Id, out var last) ? last : null, EngagementLevel(active, days));
        }).ToList();

        var weekly = days > 30;
        var buckets = Periods.Buckets(range, weekly);
        var engagement = new ChartData("engagement", buckets.Select(b => b.Label).ToList(),
            [new(weekly ? "Active clients per week" : "Active clients per day",
                buckets.Select(b => (decimal?)activeDates.Where(x => b.Contains(x.Date)).Select(x => x.ClientId).Distinct().Count()).ToList(),
                "bar")],
            "clients");

        var plans = IsAdmin ? db.WorkoutPlans : db.WorkoutPlans.Where(p => p.TrainerId == UserId);
        var exercises = await plans.SelectMany(p => p.Exercises.Select(e => new { e.Name, p.Id, p.ClientId })).ToListAsync();
        var appointments = IsAdmin ? db.Appointments : db.Appointments.Where(a => a.TrainerId == UserId);
        var statusCounts = await appointments.Where(a => a.StartsAt >= fromTime && a.StartsAt < to.AddDays(1).ToDateTime(TimeOnly.MinValue))
            .GroupBy(a => a.Status).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        return new ReportsViewModel
        {
            Days = days,
            Range = range,
            RangeError = rangeError,
            IsAdmin = IsAdmin,
            TotalClients = rows.Count,
            ActiveClients = rows.Count(r => r.ActiveDays > 0),
            AverageActiveDaysPerWeek = rows.Count == 0 ? 0 : Math.Round((decimal)rows.Average(r => r.ActiveDays) * 7m / days, 1),
            TotalEntries = workouts.Count + meals.Count + measurements.Count,
            AppointmentsCompleted = statusCounts.GetValueOrDefault(AppointmentStatus.Completed),
            Clients = rows.OrderByDescending(r => r.ActiveDays).ThenBy(r => r.FullName).ToList(),
            Engagement = engagement,
            TopExercises = exercises
                .GroupBy(e => e.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => new ExerciseUsage(g.First().Name.Trim(), g.Select(e => e.Id).Distinct().Count(), g.Select(e => e.ClientId).Distinct().Count()))
                .OrderByDescending(e => e.Plans).ThenByDescending(e => e.Clients).ThenBy(e => e.Name).Take(8).ToList(),
            TopActivities = workouts
                .GroupBy(w => w.Activity.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => new ActivitySummary(g.First().Activity.Trim(), g.Count(), g.Sum(w => w.DurationMinutes), g.Select(w => w.ClientId).Distinct().Count()))
                .OrderByDescending(a => a.Sessions).ThenByDescending(a => a.Minutes).Take(8).ToList(),
            Appointments = Enum.GetValues<AppointmentStatus>()
                .Select(s => new StatusCount(s.ToString(), statusCounts.GetValueOrDefault(s))).ToList()
        };
    }
}
