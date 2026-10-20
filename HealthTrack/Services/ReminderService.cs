using HealthTrack.Data;
using HealthTrack.Helpers;
using HealthTrack.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HealthTrack.Services;

public class ReminderOptions
{
    public const string Section = "Reminders";

    public int CheckIntervalMinutes { get; set; } = 5;
    public int AppointmentHoursBefore { get; set; } = 24;
    public TimeOnly WorkoutTime { get; set; } = new(17, 0);
    public TimeOnly MealTime { get; set; } = new(20, 0);
    public int WindowHours { get; set; } = 4;
}

// Periodically creates appointment, workout and meal reminders. Each reminder has a key, so it's only sent once.
public class ReminderService(IServiceScopeFactory scopes, IOptions<ReminderOptions> options, ILogger<ReminderService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, options.Value.CheckIntervalMinutes)));
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Reminder run failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken token)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<NotificationService>();
        var settings = options.Value;
        var now = DateTime.Now;
        var today = Dates.Today;

        var horizon = now.AddHours(settings.AppointmentHoursBefore);
        var appointments = await db.Appointments.AsNoTracking().Include(a => a.Client).Include(a => a.Trainer)
            .Where(a => a.Status == AppointmentStatus.Approved && a.StartsAt > now && a.StartsAt <= horizon)
            .ToListAsync(token);
        foreach (var a in appointments)
        {
            var key = $"appt-reminder:{a.Id}:{a.StartsAt:yyyyMMddHHmm}";
            var when = a.StartsAt.Date == DateTime.Today ? $"today at {a.StartsAt:HH:mm}" : $"{a.StartsAt:dddd d MMM 'at' HH:mm}";
            var link = $"/Appointments/Details/{a.Id}";
            await notifications.NotifyAsync(a.ClientId, NotificationType.AppointmentReminder, "Upcoming appointment",
                $"Reminder: your {a.Mode.Label().ToLowerInvariant()} appointment with {a.Trainer!.FullName} is {when}.", link, key);
            await notifications.NotifyAsync(a.TrainerId, NotificationType.AppointmentReminder, "Upcoming appointment",
                $"Reminder: your {a.Mode.Label().ToLowerInvariant()} appointment with {a.Client!.FullName} is {when}.", link, key);
        }

        if (InWindow(now, settings.WorkoutTime, settings.WindowHours))
        {
            var day = today.DayOfWeek;
            var clients = await db.UsersInRole(Roles.Client)
                .Where(u => u.RemindWorkouts
                    && db.WorkoutPlans.Any(p => p.ClientId == u.Id && p.IsActive && p.StartDate <= today
                        && (p.EndDate == null || p.EndDate >= today) && p.Exercises.Any(e => e.Day == null || e.Day == day))
                    && !db.WorkoutLogs.Any(l => l.ClientId == u.Id && l.Date == today))
                .Select(u => u.Id).ToListAsync(token);
            foreach (var id in clients)
                await notifications.NotifyAsync(id, NotificationType.WorkoutReminder, "Time for today's workout",
                    "Your workout plan has exercises for today and you haven't logged a workout yet.",
                    "/WorkoutLogs", $"workout:{today:yyyy-MM-dd}");
        }

        if (InWindow(now, settings.MealTime, settings.WindowHours))
        {
            var clients = await db.UsersInRole(Roles.Client)
                .Where(u => u.RemindMeals
                    && db.DietPlans.Any(p => p.ClientId == u.Id && p.IsActive && p.StartDate <= today && (p.EndDate == null || p.EndDate >= today))
                    && !db.MealLogs.Any(l => l.ClientId == u.Id && l.Date == today))
                .Select(u => u.Id).ToListAsync(token);
            foreach (var id in clients)
                await notifications.NotifyAsync(id, NotificationType.MealReminder, "Log today's meals",
                    "You haven't logged any meals today. Keeping your meal log up to date helps your trainer.",
                    "/MealLogs", $"meal:{today:yyyy-MM-dd}");
        }
    }

    private static bool InWindow(DateTime now, TimeOnly start, int hours)
    {
        var time = TimeOnly.FromDateTime(now);
        return time >= start && time < start.AddHours(hours) && start.AddHours(hours) > start;
    }
}
