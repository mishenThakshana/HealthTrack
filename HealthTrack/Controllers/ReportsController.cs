using System.Globalization;
using System.Text;
using HealthTrack.Data;
using HealthTrack.Helpers;
using HealthTrack.Models;
using HealthTrack.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers;

[Authorize(Roles = Roles.Trainer + "," + Roles.Admin)]
public class ReportsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager) : Controller
{
    private string UserId => userManager.GetUserId(User)!;
    private bool IsAdmin => User.IsInRole(Roles.Admin);

    public async Task<IActionResult> Index(int days = 30) => View(await BuildAsync(NormalizeDays(days)));

    public async Task<IActionResult> Export(int days = 30)
    {
        days = NormalizeDays(days);
        var report = await BuildAsync(days);

        string[] trainerHeader = IsAdmin ? ["Trainer"] : [];
        string[] header =
        [
            "Client", "Email", .. trainerHeader, "Workouts", "Workout minutes", "Calories burned",
            "Avg calories eaten per logged day", "Measurements", "Weight change (kg)", "Active days", "Last activity", "Engagement"
        ];

        var csv = new StringBuilder();
        csv.AppendLine(string.Join(',', header.Select(Csv)));
        foreach (var c in report.Clients)
        {
            string[] trainer = IsAdmin ? [c.TrainerName ?? "Unassigned"] : [];
            string[] row =
            [
                c.FullName, c.Email, .. trainer,
                c.Workouts.ToString(), c.Minutes.ToString(), c.CaloriesBurned.ToString(),
                c.AverageCaloriesEaten?.ToString() ?? "", c.Measurements.ToString(), c.WeightChange?.ToString("0.0", CultureInfo.InvariantCulture) ?? "",
                c.ActiveDays.ToString(), c.LastActivity?.ToString("yyyy-MM-dd") ?? "", c.Engagement
            ];
            csv.AppendLine(string.Join(',', row.Select(Csv)));
        }

        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(),
            "text/csv", $"healthtrack-client-report-{Dates.Today:yyyy-MM-dd}-{days}d.csv");
    }

    private static int NormalizeDays(int days) => Periods.Ranges.Contains(days) ? days : 30;

    private static string Csv(string value)
    {
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0])
            && !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
            value = "'" + value;
        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
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

    private async Task<ReportsViewModel> BuildAsync(int days)
    {
        var from = Periods.From(days);
        var today = Dates.Today;
        var fromTime = from.ToDateTime(TimeOnly.MinValue);

        var clientQuery = IsAdmin ? db.UsersInRole(Roles.Client) : db.Users.Where(u => u.TrainerId == UserId);
        var clients = await clientQuery.OrderBy(u => u.FullName)
            .Select(u => new { u.Id, u.FullName, u.Email, TrainerName = u.Trainer != null ? u.Trainer.FullName : null })
            .ToListAsync();
        var ids = clients.Select(c => c.Id).ToList();

        var workouts = await db.WorkoutLogs.Where(l => ids.Contains(l.ClientId) && l.Date >= from && l.Date <= today)
            .Select(l => new { l.ClientId, l.Date, l.Activity, l.DurationMinutes, l.CaloriesBurned }).ToListAsync();
        var meals = await db.MealLogs.Where(l => ids.Contains(l.ClientId) && l.Date >= from && l.Date <= today)
            .Select(l => new { l.ClientId, l.Date, l.Calories }).ToListAsync();
        var measurements = await db.HealthMeasurements.Where(m => ids.Contains(m.ClientId) && m.Date >= from && m.Date <= today)
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
        var buckets = Periods.Buckets(days, weekly);
        var engagement = new ChartData("engagement", buckets.Select(b => b.Label).ToList(),
            [new(weekly ? "Active clients per week" : "Active clients per day",
                buckets.Select(b => (decimal?)activeDates.Where(x => b.Contains(x.Date)).Select(x => x.ClientId).Distinct().Count()).ToList(),
                "bar")],
            "clients");

        var plans = IsAdmin ? db.WorkoutPlans : db.WorkoutPlans.Where(p => p.TrainerId == UserId);
        var exercises = await plans.SelectMany(p => p.Exercises.Select(e => new { e.Name, p.Id, p.ClientId })).ToListAsync();
        var appointments = IsAdmin ? db.Appointments : db.Appointments.Where(a => a.TrainerId == UserId);
        var statusCounts = await appointments.Where(a => a.StartsAt >= fromTime && a.StartsAt < today.AddDays(1).ToDateTime(TimeOnly.MinValue))
            .GroupBy(a => a.Status).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        return new ReportsViewModel
        {
            Days = days,
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
