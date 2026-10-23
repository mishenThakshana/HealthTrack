using HealthTrack.Data;
using HealthTrack.Helpers;
using HealthTrack.Models;
using HealthTrack.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers;

[Authorize]
public class DashboardController(ApplicationDbContext db, UserManager<ApplicationUser> userManager) : Controller
{
    public IActionResult Index()
    {
        if (User.IsInRole(Roles.Admin)) return RedirectToAction(nameof(Admin));
        if (User.IsInRole(Roles.Trainer)) return RedirectToAction(nameof(Trainer));
        if (User.IsInRole(Roles.Client)) return RedirectToAction(nameof(Client));
        return RedirectToAction("AccessDenied", "Account");
    }

    [Authorize(Roles = Roles.Client)]
    public async Task<IActionResult> Client()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return Challenge();
        await db.Entry(user).Reference(u => u.ClientProfile).LoadAsync();

        var today = Dates.Today;
        var weekStart = Dates.StartOfWeek;

        var latest = await db.HealthMeasurements.Where(m => m.ClientId == user.Id)
            .OrderByDescending(m => m.Date).ThenByDescending(m => m.Id).FirstOrDefaultAsync();
        var height = latest?.HeightCm ?? await db.HealthMeasurements
            .Where(m => m.ClientId == user.Id && m.HeightCm != null)
            .OrderByDescending(m => m.Date).ThenByDescending(m => m.Id)
            .Select(m => m.HeightCm).FirstOrDefaultAsync();

        var now = DateTime.Now;
        var weekWorkouts = db.WorkoutLogs.Where(l => l.ClientId == user.Id && l.Date >= weekStart);

        return View(new ClientDashboardViewModel
        {
            Profile = ToProfile(user, canAddDetails: false),
            Trainer = await ProvidersController.Cards(db, db.Users.Where(u => u.Id == user.TrainerId)).FirstOrDefaultAsync(),
            ReviewPrompts = await db.Appointments
                .Where(a => a.ClientId == user.Id
                    && (a.Status == AppointmentStatus.Completed || (a.Status == AppointmentStatus.Approved && a.EndsAt <= now))
                    && !db.AppointmentReviews.Any(r => r.AppointmentId == a.Id))
                .OrderByDescending(a => a.StartsAt).Take(3)
                .Select(a => new ReviewPrompt(a.Id, a.Trainer!.FullName, a.StartsAt))
                .ToListAsync(),
            LatestMeasurement = latest,
            Bmi = latest is null ? null : new HealthMeasurement { WeightKg = latest.WeightKg, HeightCm = height }.Bmi,
            WorkoutsThisWeek = await weekWorkouts.CountAsync(),
            MinutesThisWeek = await weekWorkouts.SumAsync(l => l.DurationMinutes),
            CaloriesToday = await db.MealLogs.Where(l => l.ClientId == user.Id && l.Date == today).SumAsync(l => l.Calories ?? 0),
            Plan = await PlanSchedule.ForClientAsync(db, user.Id),
            RecentWorkouts = await db.WorkoutLogs.Where(l => l.ClientId == user.Id)
                .OrderByDescending(l => l.Date).ThenByDescending(l => l.Id).Take(5).ToListAsync(),
            UpcomingAppointments = await db.Appointments.Include(a => a.Trainer)
                .Where(a => a.ClientId == user.Id && a.StartsAt > now
                    && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Approved))
                .OrderBy(a => a.StartsAt).Take(3).ToListAsync(),
            LatestFeedback = await db.ProgressFeedback.Where(f => f.ClientId == user.Id)
                .OrderByDescending(f => f.CreatedAt).Take(2)
                .Select(f => new FeedbackItem(f.Id, f.AuthorId, f.Author!.FullName, "", f.Message, f.CreatedAt))
                .ToListAsync()
        });
    }

    [Authorize(Roles = Roles.Trainer)]
    public async Task<IActionResult> Trainer()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return Challenge();
        await db.Entry(user).Reference(u => u.TrainerProfile).Query().Include(p => p.Qualifications).LoadAsync();

        var clients = await ClientsController.GetClientSummariesAsync(db, user.Id);
        var clientIds = clients.Select(c => c.Id).ToList();
        var weekStart = Dates.StartOfWeek;
        var now = DateTime.Now;
        var tomorrow = DateTime.Today.AddDays(1);

        return View(new TrainerDashboardViewModel
        {
            Profile = ToProfile(user, rating: await RatingOfAsync(user.Id)),
            Clients = clients,
            ActivePlans = await db.WorkoutPlans.CountAsync(p => p.TrainerId == user.Id && p.IsActive)
                + await db.DietPlans.CountAsync(p => p.TrainerId == user.Id && p.IsActive),
            ClientWorkoutsThisWeek = await db.WorkoutLogs.CountAsync(l => clientIds.Contains(l.ClientId) && l.Date >= weekStart),
            TodaysAppointments = await db.Appointments.Include(a => a.Client)
                .Where(a => a.TrainerId == user.Id
                    && (a.Status == AppointmentStatus.Approved || a.Status == AppointmentStatus.Completed)
                    && a.StartsAt >= DateTime.Today && a.StartsAt < tomorrow)
                .OrderBy(a => a.StartsAt).ToListAsync(),
            AppointmentRequests = await db.Appointments.Include(a => a.Client)
                .Where(a => a.TrainerId == user.Id && a.Status == AppointmentStatus.Pending && a.StartsAt > now)
                .OrderBy(a => a.StartsAt).Take(5).ToListAsync()
        });
    }

    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Admin()
    {
        var roleCounts = await (
                from ur in db.UserRoles
                join r in db.Roles on ur.RoleId equals r.Id
                group ur by r.Name into g
                select new { Role = g.Key!, Count = g.Count() })
            .ToDictionaryAsync(x => x.Role, x => x.Count);

        var weekAgo = DateTime.UtcNow.AddDays(-7);
        var now = DateTimeOffset.UtcNow;

        var recentUsers = await db.Users
            .OrderByDescending(u => u.CreatedAt)
            .Take(8)
            .Select(u => new RecentUser(
                u.FullName,
                u.Email!,
                (from ur in db.UserRoles
                 join r in db.Roles on ur.RoleId equals r.Id
                 where ur.UserId == u.Id
                 select r.Name).FirstOrDefault() ?? "None",
                u.CreatedAt,
                u.LastLoginAt))
            .ToListAsync();

        var usageFrom = Dates.Today.AddDays(-13);
        var usageStart = usageFrom.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
        var usageDays = Enumerable.Range(0, 14).Select(i => usageFrom.AddDays(i)).ToList();

        async Task<ChartSeries> UsageAsync(string label, IQueryable<DateTime> created)
        {
            var dates = await created.Where(c => c >= usageStart).ToListAsync();
            var perDay = dates.GroupBy(c => DateOnly.FromDateTime(c.ToLocalTime())).ToDictionary(g => g.Key, g => g.Count());
            return new ChartSeries(label, usageDays.Select(d => (decimal?)perDay.GetValueOrDefault(d)).ToList(), "bar");
        }

        var usage = new ChartData("usage", usageDays.Select(d => d.ToString("d MMM")).ToList(),
        [
            await UsageAsync("Workouts", db.WorkoutLogs.Select(l => l.CreatedAt)),
            await UsageAsync("Meals", db.MealLogs.Select(l => l.CreatedAt)),
            await UsageAsync("Measurements", db.HealthMeasurements.Select(m => m.CreatedAt)),
            await UsageAsync("Appointments", db.Appointments.Select(a => a.CreatedAt))
        ], "entries per day", Stacked: true);

        var usageTotals = new List<UsageTotal>
        {
            new("bi-activity", "Workouts logged", await db.WorkoutLogs.CountAsync(), await db.WorkoutLogs.CountAsync(l => l.CreatedAt >= weekAgo)),
            new("bi-egg-fried", "Meals logged", await db.MealLogs.CountAsync(), await db.MealLogs.CountAsync(l => l.CreatedAt >= weekAgo)),
            new("bi-rulers", "Measurements", await db.HealthMeasurements.CountAsync(), await db.HealthMeasurements.CountAsync(m => m.CreatedAt >= weekAgo)),
            new("bi-clipboard-check", "Plans created",
                await db.WorkoutPlans.CountAsync() + await db.DietPlans.CountAsync(),
                await db.WorkoutPlans.CountAsync(p => p.CreatedAt >= weekAgo) + await db.DietPlans.CountAsync(p => p.CreatedAt >= weekAgo)),
            new("bi-calendar-check", "Appointments booked", await db.Appointments.CountAsync(), await db.Appointments.CountAsync(a => a.CreatedAt >= weekAgo)),
            new("bi-chat-left-text", "Feedback given", await db.ProgressFeedback.CountAsync(), await db.ProgressFeedback.CountAsync(f => f.CreatedAt >= weekAgo))
        };

        return View(new AdminDashboardViewModel
        {
            TotalUsers = await db.Users.CountAsync(),
            Clients = roleCounts.GetValueOrDefault(Roles.Client),
            Trainers = roleCounts.GetValueOrDefault(Roles.Trainer),
            ActiveThisWeek = await db.Users.CountAsync(u => u.LastLoginAt >= weekAgo),
            Usage = usage,
            UsageTotals = usageTotals,
            NewThisWeek = await db.Users.CountAsync(u => u.CreatedAt >= weekAgo),
            LockedOut = await db.Users.CountAsync(u => u.LockoutEnd != null && u.LockoutEnd > now),
            RecentUsers = recentUsers,
            AssignedClients = await db.UsersInRole(Roles.Client).CountAsync(u => u.TrainerId != null),
            TrainerLoads = await db.UsersInRole(Roles.Trainer)
                .OrderByDescending(t => db.Users.Count(c => c.TrainerId == t.Id)).ThenBy(t => t.FullName)
                .Select(t => new TrainerLoad(t.Id, t.FullName, db.Users.Count(c => c.TrainerId == t.Id)))
                .ToListAsync()
        });
    }

    private async Task<RatingSummary> RatingOfAsync(string providerId)
    {
        var reviews = db.AppointmentReviews.Where(r => r.ProviderId == providerId);
        return new RatingSummary(await reviews.AverageAsync(r => (double?)r.Rating), await reviews.CountAsync());
    }

    private static MemberDashboardViewModel ToProfile(ApplicationUser user, bool canAddDetails = true, RatingSummary? rating = null)
    {
        var details = ProfileOptions.Details(user);

        return new MemberDashboardViewModel
        {
            Avatar = Avatar.Of(user, "ht-avatar-lg"),
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            Age = Dates.Age(user.DateOfBirth),
            Gender = user.Gender,
            MemberSince = user.CreatedAt,
            Details = details,
            DetailsMissing = canAddDetails && details.Count == 0,
            Rating = rating
        };
    }
}
