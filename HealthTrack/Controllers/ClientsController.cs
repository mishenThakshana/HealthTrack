using HealthTrack.Data;
using HealthTrack.Helpers;
using HealthTrack.Models;
using HealthTrack.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers;

[Authorize(Roles = Roles.Trainer)]
public class ClientsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, ILogger<ClientsController> logger) : Controller
{
    public async Task<IActionResult> Index()
    {
        var trainerId = userManager.GetUserId(User)!;
        return View(await GetClientSummariesAsync(db, trainerId));
    }

    public async Task<IActionResult> Details(string id)
    {
        var trainerId = userManager.GetUserId(User)!;
        var client = await db.Users.Include(u => u.ClientProfile).FirstOrDefaultAsync(u => u.Id == id && u.TrainerId == trainerId);
        if (client is null)
            return NotFound();

        logger.LogInformation("Trainer {TrainerId} viewed details of client {ClientId}", trainerId, id);
        return View(new ClientDetailsViewModel
        {
            Client = client,
            Age = Dates.Age(client.DateOfBirth),
            Measurements = await db.HealthMeasurements.Where(m => m.ClientId == id)
                .OrderByDescending(m => m.Date).ThenByDescending(m => m.Id).Take(10).ToListAsync(),
            WorkoutLogs = await db.WorkoutLogs.Where(l => l.ClientId == id)
                .OrderByDescending(l => l.Date).ThenByDescending(l => l.Id).Take(10).ToListAsync(),
            MealLogs = await db.MealLogs.Where(l => l.ClientId == id)
                .OrderByDescending(l => l.Date).ThenByDescending(l => l.Id).Take(10).ToListAsync(),
            WorkoutPlans = await db.WorkoutPlans.Where(p => p.ClientId == id && p.TrainerId == trainerId)
                .OrderByDescending(p => p.IsActive).ThenByDescending(p => p.StartDate)
                .Select(p => new PlanListItem(p.Id, p.Title, client.FullName, "", p.StartDate, p.EndDate, p.IsActive, p.Exercises.Count))
                .ToListAsync(),
            DietPlans = await db.DietPlans.Where(p => p.ClientId == id && p.TrainerId == trainerId)
                .OrderByDescending(p => p.IsActive).ThenByDescending(p => p.StartDate)
                .Select(p => new PlanListItem(p.Id, p.Title, client.FullName, "", p.StartDate, p.EndDate, p.IsActive, p.Meals.Count))
                .ToListAsync()
        });
    }

    public static async Task<List<ClientSummary>> GetClientSummariesAsync(ApplicationDbContext db, string trainerId)
    {
        var weekStart = Dates.StartOfWeek;
        var clients = await db.Users
            .Where(u => u.TrainerId == trainerId)
            .OrderBy(u => u.FullName)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.Email,
                u.DateOfBirth,
                u.PhotoUpdatedAt,
                LatestWeight = db.HealthMeasurements.Where(m => m.ClientId == u.Id)
                    .OrderByDescending(m => m.Date).ThenByDescending(m => m.Id)
                    .Select(m => (decimal?)m.WeightKg).FirstOrDefault(),
                WorkoutsThisWeek = db.WorkoutLogs.Count(l => l.ClientId == u.Id && l.Date >= weekStart),
                ActivePlans = db.WorkoutPlans.Count(p => p.ClientId == u.Id && p.TrainerId == trainerId && p.IsActive)
                    + db.DietPlans.Count(p => p.ClientId == u.Id && p.TrainerId == trainerId && p.IsActive),
                LastWorkout = db.WorkoutLogs.Where(l => l.ClientId == u.Id).Max(l => (DateOnly?)l.Date),
                LastMeal = db.MealLogs.Where(l => l.ClientId == u.Id).Max(l => (DateOnly?)l.Date),
                LastMeasurement = db.HealthMeasurements.Where(m => m.ClientId == u.Id).Max(m => (DateOnly?)m.Date)
            })
            .ToListAsync();

        return clients.Select(c => new ClientSummary(
                c.Id, c.FullName, c.Email ?? string.Empty, Dates.Age(c.DateOfBirth), c.LatestWeight,
                c.WorkoutsThisWeek, c.ActivePlans,
                new[] { c.LastWorkout, c.LastMeal, c.LastMeasurement }.Max(), c.PhotoUpdatedAt))
            .ToList();
    }
}
