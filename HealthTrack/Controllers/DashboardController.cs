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

        var today = Dates.Today;
        var weekStart = Dates.StartOfWeek;

        var latest = await db.HealthMeasurements.Where(m => m.ClientId == user.Id)
            .OrderByDescending(m => m.Date).ThenByDescending(m => m.Id).FirstOrDefaultAsync();
        var height = latest?.HeightCm ?? await db.HealthMeasurements
            .Where(m => m.ClientId == user.Id && m.HeightCm != null)
            .OrderByDescending(m => m.Date).ThenByDescending(m => m.Id)
            .Select(m => m.HeightCm).FirstOrDefaultAsync();

        var weekWorkouts = db.WorkoutLogs.Where(l => l.ClientId == user.Id && l.Date >= weekStart);

        return View(new ClientDashboardViewModel
        {
            Profile = ToProfile(user),
            TrainerName = await db.Users.Where(u => u.Id == user.TrainerId).Select(u => u.FullName).FirstOrDefaultAsync(),
            LatestMeasurement = latest,
            Bmi = latest is null ? null : new HealthMeasurement { WeightKg = latest.WeightKg, HeightCm = height }.Bmi,
            WorkoutsThisWeek = await weekWorkouts.CountAsync(),
            MinutesThisWeek = await weekWorkouts.SumAsync(l => l.DurationMinutes),
            CaloriesToday = await db.MealLogs.Where(l => l.ClientId == user.Id && l.Date == today).SumAsync(l => l.Calories ?? 0),
            WorkoutPlan = await db.WorkoutPlans.Include(p => p.Exercises).Include(p => p.Trainer)
                .Where(p => p.ClientId == user.Id && p.IsActive && p.StartDate <= today)
                .OrderByDescending(p => p.StartDate).FirstOrDefaultAsync(),
            DietPlan = await db.DietPlans.Include(p => p.Meals).Include(p => p.Trainer)
                .Where(p => p.ClientId == user.Id && p.IsActive && p.StartDate <= today)
                .OrderByDescending(p => p.StartDate).FirstOrDefaultAsync(),
            RecentWorkouts = await db.WorkoutLogs.Where(l => l.ClientId == user.Id)
                .OrderByDescending(l => l.Date).ThenByDescending(l => l.Id).Take(5).ToListAsync()
        });
    }

    [Authorize(Roles = Roles.Trainer)]
    public async Task<IActionResult> Trainer()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return Challenge();

        var clients = await ClientsController.GetClientSummariesAsync(db, user.Id);
        var clientIds = clients.Select(c => c.Id).ToList();
        var weekStart = Dates.StartOfWeek;

        return View(new TrainerDashboardViewModel
        {
            Profile = ToProfile(user),
            Clients = clients,
            ActivePlans = await db.WorkoutPlans.CountAsync(p => p.TrainerId == user.Id && p.IsActive)
                + await db.DietPlans.CountAsync(p => p.TrainerId == user.Id && p.IsActive),
            ClientWorkoutsThisWeek = await db.WorkoutLogs.CountAsync(l => clientIds.Contains(l.ClientId) && l.Date >= weekStart)
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
                u.CreatedAt))
            .ToListAsync();

        return View(new AdminDashboardViewModel
        {
            TotalUsers = await db.Users.CountAsync(),
            Clients = roleCounts.GetValueOrDefault(Roles.Client),
            Trainers = roleCounts.GetValueOrDefault(Roles.Trainer),
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

    private static MemberDashboardViewModel ToProfile(ApplicationUser user) => new()
    {
        FullName = user.FullName,
        Email = user.Email ?? string.Empty,
        Age = Dates.Age(user.DateOfBirth),
        Gender = user.Gender,
        MemberSince = user.CreatedAt
    };
}
