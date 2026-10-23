using HealthTrack.Data;
using HealthTrack.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HealthTrack.Services;

public enum NotificationCategory
{
    Appointments,
    Reminders,
    Updates
}

public class NotificationService(
    ApplicationDbContext db,
    EmailQueue emails,
    IOptions<EmailOptions> options,
    ILogger<NotificationService> logger)
{
    public static NotificationCategory CategoryOf(NotificationType type) => type switch
    {
        NotificationType.WorkoutReminder or NotificationType.MealReminder => NotificationCategory.Reminders,
        NotificationType.PlanAssigned or NotificationType.FeedbackReceived or NotificationType.TrainerAssigned
            or NotificationType.ProfessionalChosen or NotificationType.ClientLeft or NotificationType.ReviewReceived => NotificationCategory.Updates,
        _ => NotificationCategory.Appointments
    };

    public static string Icon(NotificationType type) => type switch
    {
        NotificationType.AppointmentRequested => "bi-calendar-plus",
        NotificationType.AppointmentApproved => "bi-calendar-check",
        NotificationType.AppointmentDeclined or NotificationType.AppointmentCancelled => "bi-calendar-x",
        NotificationType.AppointmentRescheduled => "bi-calendar-event",
        NotificationType.AppointmentCompleted => "bi-check2-circle",
        NotificationType.AppointmentReminder => "bi-alarm",
        NotificationType.WorkoutReminder => "bi-activity",
        NotificationType.MealReminder => "bi-egg-fried",
        NotificationType.PlanAssigned => "bi-clipboard-check",
        NotificationType.FeedbackReceived => "bi-chat-left-text",
        NotificationType.ProfessionalChosen => "bi-hospital",
        NotificationType.ClientLeft => "bi-person-dash",
        NotificationType.ReviewReceived => "bi-star",
        _ => "bi-person-badge"
    };

    // Call after the caller has saved its own changes: this saves the shared DbContext.
    public async Task NotifyAsync(string userId, NotificationType type, string title, string message, string? link = null, string? key = null)
    {
        if (key is not null && await db.Notifications.AnyAsync(n => n.UserId == userId && n.Key == key))
            return;

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
            return;

        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Title = Truncate(title, 150),
            Message = Truncate(message, 500),
            Link = link,
            Key = key
        };
        db.Notifications.Add(notification);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException) when (key is not null)
        {
            db.Entry(notification).State = EntityState.Detached;
            return;
        }

        if (!WantsEmail(user, type) || user.Email is null)
            return;

        var url = link is null ? null : options.Value.AppUrl.TrimEnd('/') + link;
        var (html, text) = EmailTemplates.Render(user.FullName, notification.Title, [notification.Message],
            url is null ? null : "View in HealthTrack", url,
            "You can change which emails you get under My account → Notifications.");
        emails.Enqueue(new EmailMessage(user.Email, user.FullName, notification.Title, html, text));
        logger.LogInformation("Queued {Type} email for {UserId}", type, userId);
    }

    private static bool WantsEmail(ApplicationUser user, NotificationType type) =>
        user.EmailConfirmed && CategoryOf(type) switch
        {
            NotificationCategory.Appointments => user.EmailAppointments,
            NotificationCategory.Reminders => user.EmailReminders,
            _ => user.EmailUpdates
        };

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..(length - 1)] + "…";
}
