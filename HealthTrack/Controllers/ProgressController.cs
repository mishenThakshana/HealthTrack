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

[Authorize(Roles = Roles.Client + "," + Roles.Trainer)]
public class ProgressController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    NotificationService notifications,
    ILogger<ProgressController> logger) : Controller
{
    private string UserId => userManager.GetUserId(User)!;
    private bool IsClient => User.IsInRole(Roles.Client);

    // A preset period (days) or a custom one (from and to, both needed).
    public async Task<IActionResult> Index(string? id, int days = Periods.DefaultRange, DateOnly? from = null, DateOnly? to = null)
    {
        if (!IsClient && string.IsNullOrEmpty(id))
            return RedirectToAction("Index", "Clients");

        var client = await FindClientAsync(id);
        if (client is null)
            return NotFound();

        if (!IsClient)
            logger.LogInformation("Provider {ViewerId} viewed progress of client {ClientId}", UserId, client.Id);

        var range = Periods.Resolve(days, from, to, Periods.DefaultRange, out var rangeError);
        return View(await BuildAsync(client, range, new FeedbackFormViewModel(), rangeError));
    }

    // Downloads the progress report (the page's summary plus every entry in the period) as an Excel workbook or a PDF.
    public async Task<IActionResult> Export(string? id, int days = Periods.DefaultRange, DateOnly? from = null, DateOnly? to = null,
        string format = ReportExport.Excel)
    {
        if (!ReportExport.IsFormat(format))
            return NotFound();

        var client = await FindClientAsync(id);
        if (client is null)
            return NotFound();

        // Invalid custom dates can't be fixed from a download link, so they're refused rather than silently replaced.
        var range = Periods.Resolve(days, from, to, Periods.DefaultRange, out var rangeError);
        if (rangeError is not null)
            return BadRequest(rangeError);

        var progress = await BuildAsync(client, range, new FeedbackFormViewModel());
        (var start, var end) = (range.From, range.To);

        var measurements = await db.HealthMeasurements.Where(m => m.ClientId == client.Id && m.Date >= start && m.Date <= end)
            .OrderBy(m => m.Date).ThenBy(m => m.Id).ToListAsync();
        // Height is only entered now and then, so each BMI uses the latest height known on that date.
        var height = await db.HealthMeasurements.Where(m => m.ClientId == client.Id && m.Date < start && m.HeightCm != null)
            .OrderByDescending(m => m.Date).ThenByDescending(m => m.Id).Select(m => m.HeightCm).FirstOrDefaultAsync();
        var measurementRows = new List<IReadOnlyList<object?>>();
        foreach (var m in measurements)
        {
            height = m.HeightCm ?? height;
            measurementRows.Add([m.Date, m.WeightKg, m.HeightCm, new HealthMeasurement { WeightKg = m.WeightKg, HeightCm = height }.Bmi,
                m.BodyFatPercent, m.RestingHeartRate, m.Notes]);
        }

        var workouts = await db.WorkoutLogs.Where(l => l.ClientId == client.Id && l.Date >= start && l.Date <= end)
            .OrderBy(l => l.Date).ThenBy(l => l.Id).ToListAsync();
        var meals = await db.MealLogs.Where(l => l.ClientId == client.Id && l.Date >= start && l.Date <= end)
            .OrderBy(l => l.Date).ThenBy(l => l.MealType).ThenBy(l => l.Id).ToListAsync();

        var export = new ExportReport(
            $"Progress report: {client.FullName}",
            $"{RangeText(range)} · generated {DateTime.Now:d MMM yyyy HH:mm}",
            [
                ("Current weight (kg)", progress.CurrentWeight),
                ("Weight change (kg)", progress.WeightChange),
                ("BMI", progress.Bmi),
                ("Workouts", progress.Workouts),
                ("Workout minutes", progress.WorkoutMinutes),
                ("Calories burned", progress.CaloriesBurned),
                ("Average calories eaten per logged day", progress.AverageCaloriesEaten),
                ("Daily calorie target", progress.CalorieTarget),
                ("Active days", $"{progress.ActiveDays} of {range.Days}")
            ],
            [
                new("Measurements", ["Date", "Weight (kg)", "Height (cm)", "BMI", "Body fat (%)", "Resting heart rate (bpm)", "Notes"], measurementRows),
                new("Workouts", ["Date", "Activity", "Minutes", "Calories burned", "Notes"],
                    workouts.Select(w => (IReadOnlyList<object?>)[w.Date, w.Activity, w.DurationMinutes, w.CaloriesBurned, w.Notes]).ToList()),
                new("Meals", ["Date", "Meal", "Description", "Calories"],
                    meals.Select(m => (IReadOnlyList<object?>)[m.Date, m.MealType.ToString(), m.Description, m.Calories]).ToList()),
                new("Top activities", ["Activity", "Sessions", "Minutes"],
                    progress.TopActivities.Select(a => (IReadOnlyList<object?>)[a.Activity, a.Sessions, a.Minutes]).ToList()),
                new("Trainer feedback", ["Date", "From", "Feedback"],
                    progress.Feedback.Where(f => range.Contains(DateOnly.FromDateTime(f.CreatedAt.ToLocalTime())))
                        .Select(f => (IReadOnlyList<object?>)[f.CreatedAt.ToLocalTime(), f.AuthorName, f.Message]).ToList())
            ]);

        logger.LogInformation("User {ViewerId} exported the progress report of client {ClientId} as {Format}", UserId, client.Id, format);
        return ReportExport.ToFile(export, format, $"healthtrack-progress-{Dates.Today:yyyy-MM-dd}-{range.FileSuffix}");
    }

    [HttpPost, Authorize(Roles = Roles.Trainer), ValidateAntiForgeryToken]
    public async Task<IActionResult> AddFeedback(string id, int days, DateOnly? from, DateOnly? to, [Bind(Prefix = "FeedbackForm")] FeedbackFormViewModel form)
    {
        var client = await FindClientAsync(id);
        if (client is null)
            return NotFound();

        var range = Periods.Resolve(days, from, to, Periods.DefaultRange, out _);
        if (!ModelState.IsValid)
            return View(nameof(Index), await BuildAsync(client, range, form));

        db.ProgressFeedback.Add(new ProgressFeedback { ClientId = client.Id, AuthorId = UserId, Message = form.Message.Trim() });
        await db.SaveChangesAsync();
        logger.LogInformation("Feedback added for client {ClientId} by {AuthorId}", client.Id, UserId);
        var author = await db.Users.Where(u => u.Id == UserId).Select(u => u.FullName).FirstAsync();
        await notifications.NotifyAsync(client.Id, NotificationType.FeedbackReceived, $"New feedback from {author}",
            form.Message.Trim(), "/Progress#feedback");

        TempData["Success"] = $"Feedback shared with {client.FullName}.";
        return RedirectToAction(nameof(Index), new { id, days = range.RouteDays, from = range.RouteFrom, to = range.RouteTo });
    }

    [HttpPost, Authorize(Roles = Roles.Trainer), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteFeedback(int feedbackId, int days, DateOnly? from, DateOnly? to)
    {
        var feedback = await db.ProgressFeedback.FirstOrDefaultAsync(f => f.Id == feedbackId && f.AuthorId == UserId);
        if (feedback is null)
            return NotFound();

        db.ProgressFeedback.Remove(feedback);
        await db.SaveChangesAsync();

        TempData["Success"] = "Feedback deleted.";
        var range = Periods.Resolve(days, from, to, Periods.DefaultRange, out _);
        return RedirectToAction(nameof(Index), new { id = feedback.ClientId, days = range.RouteDays, from = range.RouteFrom, to = range.RouteTo });
    }

    private async Task<ApplicationUser?> FindClientAsync(string? id) =>
        IsClient
            ? await userManager.GetUserAsync(User)
            : await ClientAccess.VisibleClients(db, User, UserId).Include(u => u.ClientProfile).FirstOrDefaultAsync(u => u.Id == id);

    // "1 Sep 2026 – 30 Sep 2026 (30 days)", or "... (last 30 days)" for a preset.
    private static string RangeText(DateRange range) =>
        $"{range.From:d MMM yyyy} – {range.To:d MMM yyyy} ({(range.IsCustom ? "" : "last ")}{range.Days} days)";

    private async Task<ProgressViewModel> BuildAsync(ApplicationUser client, DateRange range, FeedbackFormViewModel form, string? rangeError = null)
    {
        var (from, to, days) = (range.From, range.To, range.Days);

        var measurements = await db.HealthMeasurements
            .Where(m => m.ClientId == client.Id && m.Date >= from && m.Date <= to)
            .OrderBy(m => m.Date).ThenBy(m => m.Id).ToListAsync();
        var height = await db.HealthMeasurements
            .Where(m => m.ClientId == client.Id && m.Date < from && m.HeightCm != null)
            .OrderByDescending(m => m.Date).ThenByDescending(m => m.Id)
            .Select(m => m.HeightCm).FirstOrDefaultAsync();

        var points = new List<(DateOnly Date, HealthMeasurement Entry, decimal? Bmi)>();
        foreach (var m in measurements)
        {
            height = m.HeightCm ?? height;
            var point = (m.Date, m, new HealthMeasurement { WeightKg = m.WeightKg, HeightCm = height }.Bmi);
            if (points.Count > 0 && points[^1].Date == m.Date)
                points[^1] = point;
            else
                points.Add(point);
        }

        var workouts = await db.WorkoutLogs
            .Where(l => l.ClientId == client.Id && l.Date >= from && l.Date <= to)
            .Select(l => new { l.Date, l.Activity, l.DurationMinutes, l.CaloriesBurned }).ToListAsync();
        var meals = await db.MealLogs
            .Where(l => l.ClientId == client.Id && l.Date >= from && l.Date <= to)
            .Select(l => new { l.Date, l.Calories }).ToListAsync();

        var calorieTarget = await db.DietPlans
            .Where(p => p.ClientId == client.Id && p.IsActive && p.StartDate <= to)
            .OrderByDescending(p => p.StartDate).Select(p => p.DailyCalorieTarget).FirstOrDefaultAsync();

        var weeklyActivity = days > 30;
        var activityBuckets = Periods.Buckets(range, weeklyActivity);
        var mealBuckets = Periods.Buckets(range, days > 90);
        var mealDays = meals.Where(m => m.Calories is not null).GroupBy(m => m.Date)
            .ToDictionary(g => g.Key, g => g.Sum(m => m.Calories!.Value));

        var labels = points.Select(p => p.Date.ToString(days > 180 ? "d MMM yy" : "d MMM")).ToList();
        var first = points.FirstOrDefault();
        var last = points.LastOrDefault();
        var feedback = await db.ProgressFeedback
            .Where(f => f.ClientId == client.Id)
            .OrderByDescending(f => f.CreatedAt).Take(20)
            .Select(f => new FeedbackItem(f.Id, f.AuthorId, f.Author!.FullName,
                (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id where ur.UserId == f.AuthorId select r.Name)
                    .FirstOrDefault() ?? "", f.Message, f.CreatedAt))
            .ToListAsync();

        decimal? AverageEaten(Bucket b)
        {
            var values = mealDays.Where(d => b.Contains(d.Key) && d.Value > 0).Select(d => (decimal)d.Value).ToList();
            return values.Count == 0 ? null : Math.Round(values.Average());
        }

        return new ProgressViewModel
        {
            Client = client,
            IsOwn = IsClient,
            CanGiveFeedback = !IsClient,
            CurrentUserId = UserId,
            Days = days,
            Range = range,
            RangeError = rangeError,
            CurrentWeight = last.Entry?.WeightKg,
            WeightChange = points.Count > 1 ? last.Entry!.WeightKg - first.Entry!.WeightKg : null,
            Bmi = last.Bmi,
            Workouts = workouts.Count,
            WorkoutMinutes = workouts.Sum(w => w.DurationMinutes),
            CaloriesBurned = workouts.Sum(w => w.CaloriesBurned ?? 0),
            AverageCaloriesEaten = mealDays.Count == 0 ? null : (int)Math.Round(mealDays.Values.Average()),
            CalorieTarget = calorieTarget,
            ActiveDays = workouts.Select(w => w.Date).Concat(meals.Select(m => m.Date)).Concat(measurements.Select(m => m.Date))
                .Distinct().Count(),

            Weight = new ChartData("weight", labels, [new("Weight (kg)", points.Select(p => (decimal?)p.Entry.WeightKg).ToList())], "kg"),
            BmiTrend = new ChartData("bmi", labels, [new("BMI", points.Select(p => p.Bmi).ToList())], "BMI"),
            BodyFat = new ChartData("bodyfat", labels, [new("Body fat (%)", points.Select(p => p.Entry.BodyFatPercent).ToList())], "%"),
            HeartRate = new ChartData("heartrate", labels,
                [new("Resting heart rate (bpm)", points.Select(p => (decimal?)p.Entry.RestingHeartRate).ToList())], "bpm"),
            Minutes = new ChartData("minutes", activityBuckets.Select(b => b.Label).ToList(),
                [new("Workout minutes", activityBuckets.Select(b => (decimal?)workouts.Where(w => b.Contains(w.Date)).Sum(w => w.DurationMinutes)).ToList(), "bar")],
                weeklyActivity ? "minutes per week" : "minutes per day"),
            Burned = new ChartData("burned", activityBuckets.Select(b => b.Label).ToList(),
                [new("Calories burned", activityBuckets.Select(b => (decimal?)workouts.Where(w => b.Contains(w.Date)).Sum(w => w.CaloriesBurned ?? 0)).ToList(), "bar")],
                weeklyActivity ? "kcal per week" : "kcal per day"),
            Eaten = new ChartData("eaten", mealBuckets.Select(b => b.Label).ToList(),
                [
                    new(days > 90 ? "Average kcal per logged day" : "Calories eaten", mealBuckets.Select(AverageEaten).ToList(), "bar"),
                    .. calorieTarget is { } target
                        ? new[] { new ChartSeries("Daily target", mealBuckets.Select(_ => (decimal?)target).ToList(), "reference") }
                        : []
                ],
                "kcal"),

            TopActivities = workouts
                .GroupBy(w => w.Activity.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => new ActivitySummary(g.First().Activity.Trim(), g.Count(), g.Sum(w => w.DurationMinutes)))
                .OrderByDescending(a => a.Sessions).ThenByDescending(a => a.Minutes).Take(5).ToList(),

            Feedback = feedback,
            FeedbackForm = form
        };
    }
}
