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

[Authorize(Roles = Roles.Client + "," + Roles.Providers)]
public class ProgressController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    NotificationService notifications,
    ILogger<ProgressController> logger) : Controller
{
    private string UserId => userManager.GetUserId(User)!;
    private bool IsClient => User.IsInRole(Roles.Client);

    public async Task<IActionResult> Index(string? id, int days = Periods.DefaultRange)
    {
        if (!IsClient && string.IsNullOrEmpty(id))
            return User.IsInRole(Roles.Trainer) ? RedirectToAction("Index", "Clients") : RedirectToAction("Index", "Appointments");

        var client = await FindClientAsync(id);
        if (client is null)
            return NotFound();

        if (!IsClient)
            logger.LogInformation("Provider {ViewerId} viewed progress of client {ClientId}", UserId, client.Id);

        return View(await BuildAsync(client, Periods.Normalize(days), new FeedbackFormViewModel()));
    }

    [HttpPost, Authorize(Roles = Roles.Providers), ValidateAntiForgeryToken]
    public async Task<IActionResult> AddFeedback(string id, int days, [Bind(Prefix = "FeedbackForm")] FeedbackFormViewModel form)
    {
        var client = await FindClientAsync(id);
        if (client is null)
            return NotFound();

        days = Periods.Normalize(days);
        if (!ModelState.IsValid)
            return View(nameof(Index), await BuildAsync(client, days, form));

        db.ProgressFeedback.Add(new ProgressFeedback { ClientId = client.Id, AuthorId = UserId, Message = form.Message.Trim() });
        await db.SaveChangesAsync();
        logger.LogInformation("Feedback added for client {ClientId} by {AuthorId}", client.Id, UserId);
        var author = await db.Users.Where(u => u.Id == UserId).Select(u => u.FullName).FirstAsync();
        await notifications.NotifyAsync(client.Id, NotificationType.FeedbackReceived, $"New feedback from {author}",
            form.Message.Trim(), "/Progress#feedback");

        TempData["Success"] = $"Feedback shared with {client.FullName}.";
        return RedirectToAction(nameof(Index), new { id, days });
    }

    [HttpPost, Authorize(Roles = Roles.Providers), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteFeedback(int feedbackId, int days)
    {
        var feedback = await db.ProgressFeedback.FirstOrDefaultAsync(f => f.Id == feedbackId && f.AuthorId == UserId);
        if (feedback is null)
            return NotFound();

        db.ProgressFeedback.Remove(feedback);
        await db.SaveChangesAsync();

        TempData["Success"] = "Feedback deleted.";
        return RedirectToAction(nameof(Index), new { id = feedback.ClientId, days = Periods.Normalize(days) });
    }

    private async Task<ApplicationUser?> FindClientAsync(string? id) =>
        IsClient
            ? await userManager.GetUserAsync(User)
            : await ClientAccess.VisibleClients(db, User, UserId).Include(u => u.ClientProfile).FirstOrDefaultAsync(u => u.Id == id);

    private async Task<ProgressViewModel> BuildAsync(ApplicationUser client, int days, FeedbackFormViewModel form)
    {
        var from = Periods.From(days);
        var today = Dates.Today;

        var measurements = await db.HealthMeasurements
            .Where(m => m.ClientId == client.Id && m.Date >= from && m.Date <= today)
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
            .Where(l => l.ClientId == client.Id && l.Date >= from && l.Date <= today)
            .Select(l => new { l.Date, l.Activity, l.DurationMinutes, l.CaloriesBurned }).ToListAsync();
        var meals = await db.MealLogs
            .Where(l => l.ClientId == client.Id && l.Date >= from && l.Date <= today)
            .Select(l => new { l.Date, l.Calories }).ToListAsync();

        var calorieTarget = await db.DietPlans
            .Where(p => p.ClientId == client.Id && p.IsActive && p.StartDate <= today)
            .OrderByDescending(p => p.StartDate).Select(p => p.DailyCalorieTarget).FirstOrDefaultAsync();

        var weeklyActivity = days > 30;
        var activityBuckets = Periods.Buckets(days, weeklyActivity);
        var mealBuckets = Periods.Buckets(days, days > 90);
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

            Feedback = feedback.Select(f => f with { AuthorRole = Roles.DisplayName(f.AuthorRole) }).ToList(),
            FeedbackForm = form
        };
    }
}
