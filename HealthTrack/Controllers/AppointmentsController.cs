using HealthTrack.Data;
using HealthTrack.Helpers;
using HealthTrack.Models;
using HealthTrack.Models.ViewModels;
using HealthTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers;

[Authorize(Roles = Roles.Client + "," + Roles.AppointmentManagers)]
public class AppointmentsController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    NotificationService notifications,
    ILogger<AppointmentsController> logger) : Controller
{
    private string UserId => userManager.GetUserId(User)!;
    private bool IsAdmin => User.IsInRole(Roles.Admin);

    // Trainers manage their own appointments; admins manage everyone's.
    private bool IsProvider => User.IsInRole(Roles.Trainer) || IsAdmin;

    // "with" preselects who to book, for example from the Find a trainer page.
    public async Task<IActionResult> Index(string? with)
    {
        var form = new AppointmentRequestViewModel();
        if (!IsProvider)
            form.TrainerId = with is not null && await Providers().AnyAsync(u => u.Id == with)
                ? with
                : await db.Users.Where(u => u.Id == UserId).Select(u => u.TrainerId).FirstOrDefaultAsync() ?? string.Empty;

        return View(await BuildPageAsync(form));
    }

    public async Task<IActionResult> Details(int id, bool editReview = false)
    {
        var appointment = await LoadAsync(id);
        if (appointment is null)
            return NotFound();

        var review = await db.AppointmentReviews.AsNoTracking().FirstOrDefaultAsync(r => r.AppointmentId == id);
        return View(new AppointmentDetailsViewModel
        {
            Appointment = appointment,
            IsProvider = IsProvider,
            IsAdmin = IsAdmin,
            Review = review,
            ReviewForm = new ReviewFormViewModel { Rating = review?.Rating, Comment = review?.Comment },
            EditingReview = editReview
        });
    }

    // Clients rate a completed (or approved and ended) appointment, or change the rating they gave.
    [HttpPost, Authorize(Roles = Roles.Client), ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(int id, [Bind(Prefix = "ReviewForm")] ReviewFormViewModel form)
    {
        var appointment = await LoadAsync(id);
        if (appointment is null || !AppointmentRules.CanReview(appointment))
            return NotFound();

        var review = await db.AppointmentReviews.FirstOrDefaultAsync(r => r.AppointmentId == id);
        if (!ModelState.IsValid)
            return View(nameof(Details), new AppointmentDetailsViewModel
            {
                Appointment = appointment, Review = review, ReviewForm = form, EditingReview = true
            });

        var comment = string.IsNullOrWhiteSpace(form.Comment) ? null : form.Comment.Trim();
        if (review is null)
        {
            db.AppointmentReviews.Add(new AppointmentReview
            {
                AppointmentId = id, ClientId = UserId, ProviderId = appointment.TrainerId, Rating = form.Rating!.Value, Comment = comment
            });
            await db.SaveChangesAsync();
            await notifications.NotifyAsync(appointment.TrainerId, NotificationType.ReviewReceived, "New review",
                $"{appointment.Client!.FullName} rated your appointment on {When(appointment)} {form.Rating} out of 5.", DetailsLink(appointment));
            TempData["Success"] = "Thanks for your review!";
        }
        else
        {
            (review.Rating, review.Comment, review.UpdatedAt) = (form.Rating!.Value, comment, DateTime.UtcNow);
            await db.SaveChangesAsync();
            TempData["Success"] = "Your review has been updated.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // Clients can withdraw their own review; admins can remove any review (for example, an inappropriate one).
    [HttpPost, Authorize(Roles = Roles.Client + "," + Roles.Admin), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteReview(int id, string? returnUrl)
    {
        var review = await db.AppointmentReviews.FirstOrDefaultAsync(r => r.AppointmentId == id && (IsAdmin || r.ClientId == UserId));
        if (review is null)
            return NotFound();

        db.AppointmentReviews.Remove(review);
        await db.SaveChangesAsync();
        logger.LogInformation("User {UserId} deleted the review of appointment {AppointmentId}", UserId, id);

        TempData["Success"] = "The review has been deleted.";
        return BackTo(returnUrl, id);
    }

    [HttpPost, Authorize(Roles = Roles.Client), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind(Prefix = "Form")] AppointmentRequestViewModel form)
    {
        var trainerName = await Providers()
            .Where(u => u.Id == form.TrainerId).Select(u => u.FullName).FirstOrDefaultAsync();
        if (!string.IsNullOrEmpty(form.TrainerId) && trainerName is null)
            ModelState.AddModelError("Form.TrainerId", "Please choose who you'd like to see.");

        ValidateSlot(ModelState, "Form", form);
        if (ModelState.IsValid)
        {
            if (await TrainerBusyAsync(form.TrainerId, form.StartsAt, form.EndsAt, null))
                ModelState.AddModelError("Form.Time", $"{trainerName} is already booked at that time. Please pick another time.");
            else if (await ClientBusyAsync(UserId, form.StartsAt, form.EndsAt, null))
                ModelState.AddModelError("Form.Time", "You already have an appointment at that time.");
        }

        if (!ModelState.IsValid)
            return View(nameof(Index), await BuildPageAsync(form));

        var appointment = new Appointment
        {
            ClientId = UserId,
            TrainerId = form.TrainerId,
            StartsAt = form.StartsAt,
            EndsAt = form.EndsAt,
            Mode = form.Mode,
            Reason = form.Reason.Trim()
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        await notifications.NotifyAsync(form.TrainerId, NotificationType.AppointmentRequested, "New appointment request",
            $"{await MyNameAsync()} asked for a {form.Mode.Label().ToLowerInvariant()} appointment on {When(appointment)}: “{appointment.Reason}”",
            DetailsLink(appointment));

        TempData["Success"] = $"Request sent to {trainerName}. You'll see it under Upcoming once it's approved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, Authorize(Roles = Roles.AppointmentManagers), ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string? returnUrl)
    {
        var appointment = await FindOwnAsync(id);
        if (appointment is null || !AppointmentRules.CanRespond(appointment))
            return NotFound();

        if (await TrainerBusyAsync(appointment.TrainerId, appointment.StartsAt, appointment.EndsAt, id))
        {
            TempData["Error"] = $"{(IsAdmin ? appointment.Trainer!.FullName + " already has" : "You already have")} an appointment at that time. Reschedule this request or decline it.";
            return RedirectToAction(nameof(Details), new { id });
        }

        SetStatus(appointment, AppointmentStatus.Approved);
        await db.SaveChangesAsync();
        var me = await MyNameAsync();
        await NotifyClientAsync(appointment, NotificationType.AppointmentApproved, "Appointment approved",
            whose => $"{me} approved {whose} appointment on {When(appointment)}.");

        TempData["Success"] = $"Approved {appointment.Client!.FullName}'s appointment on {appointment.StartsAt:ddd d MMM 'at' HH:mm}.";
        return BackTo(returnUrl, id);
    }

    [HttpPost, Authorize(Roles = Roles.AppointmentManagers), ValidateAntiForgeryToken]
    public async Task<IActionResult> Decline(int id, string? note, string? returnUrl)
    {
        var appointment = await FindOwnAsync(id);
        if (appointment is null || !AppointmentRules.CanRespond(appointment))
            return NotFound();

        SetStatus(appointment, AppointmentStatus.Declined);
        appointment.TrainerNote = TrackingRules.Clean(note) ?? appointment.TrainerNote;
        await db.SaveChangesAsync();
        var me = await MyNameAsync();
        await NotifyClientAsync(appointment, NotificationType.AppointmentDeclined, "Appointment declined",
            whose => $"{me} declined {whose} request for {When(appointment)}." + (TrackingRules.Clean(note) is { } reason ? $" Reason: {reason}" : ""));

        TempData["Success"] = $"Declined {appointment.Client!.FullName}'s request.";
        return BackTo(returnUrl, id);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? note)
    {
        var appointment = await FindOwnAsync(id);
        if (appointment is null || !AppointmentRules.CanCancel(appointment, IsProvider))
            return NotFound();

        SetStatus(appointment, AppointmentStatus.Cancelled);
        appointment.CancelledByClient = !IsProvider;
        appointment.CancelledByAdmin = IsAdmin;
        if (IsProvider)
            appointment.TrainerNote = TrackingRules.Clean(note) ?? appointment.TrainerNote;
        await db.SaveChangesAsync();

        var message = $"{await MyNameAsync()} cancelled the appointment on {When(appointment)}."
            + (IsProvider && TrackingRules.Clean(note) is { } reason ? $" Reason: {reason}" : "");
        string[] recipients = IsAdmin ? [appointment.ClientId, appointment.TrainerId] : [IsProvider ? appointment.ClientId : appointment.TrainerId];
        foreach (var recipient in recipients)
            await notifications.NotifyAsync(recipient, NotificationType.AppointmentCancelled, "Appointment cancelled", message, DetailsLink(appointment));

        TempData["Success"] = "Appointment cancelled.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, Authorize(Roles = Roles.AppointmentManagers), ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id, string? returnUrl)
    {
        var appointment = await FindOwnAsync(id);
        if (appointment is null || !AppointmentRules.CanComplete(appointment))
            return NotFound();

        SetStatus(appointment, AppointmentStatus.Completed);
        await db.SaveChangesAsync();
        var me = await MyNameAsync();
        await NotifyClientAsync(appointment, NotificationType.AppointmentCompleted, "Appointment completed",
            whose => $"{me} marked {whose} appointment on {When(appointment)} as completed."
                + (whose == "your" ? " Open it to leave a rating and feedback." : ""));

        TempData["Success"] = "Marked as completed.";
        return BackTo(returnUrl, id);
    }

    [Authorize(Roles = Roles.AppointmentManagers)]
    public async Task<IActionResult> Edit(int id)
    {
        var appointment = await FindOwnAsync(id);
        if (appointment is null || !AppointmentRules.CanReschedule(appointment))
            return NotFound();

        return View(new AppointmentEditPageViewModel
        {
            Appointment = appointment,
            Form = new AppointmentEditViewModel
            {
                Date = DateOnly.FromDateTime(appointment.StartsAt),
                Time = TimeOnly.FromDateTime(appointment.StartsAt),
                DurationMinutes = appointment.DurationMinutes,
                Mode = appointment.Mode,
                Location = appointment.Location,
                TrainerNote = appointment.TrainerNote
            }
        });
    }

    [HttpPost, Authorize(Roles = Roles.AppointmentManagers), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind(Prefix = "Form")] AppointmentEditViewModel form)
    {
        var appointment = await FindOwnAsync(id);
        if (appointment is null || !AppointmentRules.CanReschedule(appointment))
            return NotFound();

        ValidateSlot(ModelState, "Form", form);
        if (ModelState.IsValid)
        {
            if (await TrainerBusyAsync(appointment.TrainerId, form.StartsAt, form.EndsAt, id))
                ModelState.AddModelError("Form.Time", $"{(IsAdmin ? appointment.Trainer!.FullName + " already has" : "You already have")} an appointment at that time.");
            else if (await ClientBusyAsync(appointment.ClientId, form.StartsAt, form.EndsAt, id))
                ModelState.AddModelError("Form.Time", $"{appointment.Client!.FullName} already has an appointment at that time.");
        }

        if (!ModelState.IsValid)
            return View(new AppointmentEditPageViewModel { Appointment = appointment, Form = form });

        appointment.StartsAt = form.StartsAt;
        appointment.EndsAt = form.EndsAt;
        appointment.Mode = form.Mode;
        appointment.Location = TrackingRules.Clean(form.Location);
        appointment.TrainerNote = TrackingRules.Clean(form.TrainerNote);
        SetStatus(appointment, AppointmentStatus.Approved);
        await db.SaveChangesAsync();
        var me = await MyNameAsync();
        await NotifyClientAsync(appointment, NotificationType.AppointmentRescheduled, "Appointment updated",
            whose => $"{me} confirmed {whose} appointment for {When(appointment)} ({appointment.Mode.Label().ToLowerInvariant()})."
                + (appointment.Location is { } location ? $" Location: {location}" : ""));

        TempData["Success"] = "Appointment updated and confirmed.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private static void ValidateSlot(ModelStateDictionary modelState, string prefix, AppointmentSlotViewModel slot)
    {
        if (!AppointmentRules.Durations.Contains(slot.DurationMinutes))
            modelState.AddModelError($"{prefix}.DurationMinutes", "Please choose a duration from the list.");

        if (!Enum.IsDefined(slot.Mode))
            modelState.AddModelError($"{prefix}.Mode", "Please choose a type.");

        if (slot.Time is not { } time)
            return;

        if (slot.StartsAt <= DateTime.Now)
            modelState.AddModelError($"{prefix}.Time", "Please choose a time in the future.");
        else if (slot.Date > Dates.Today.AddDays(AppointmentRules.MaxDaysAhead))
            modelState.AddModelError($"{prefix}.Date", $"Appointments can be booked up to {AppointmentRules.MaxDaysAhead} days ahead.");
        else if (time < AppointmentRules.OpensAt || time.AddMinutes(slot.DurationMinutes) > AppointmentRules.ClosesAt
                 || time.AddMinutes(slot.DurationMinutes) < time)
            modelState.AddModelError($"{prefix}.Time",
                $"Appointments run between {AppointmentRules.OpensAt:HH:mm} and {AppointmentRules.ClosesAt:HH:mm}.");
    }

    private Task<bool> TrainerBusyAsync(string trainerId, DateTime start, DateTime end, int? exceptId) =>
        db.Appointments.AnyAsync(a => a.TrainerId == trainerId && a.Id != exceptId
            && a.Status == AppointmentStatus.Approved && a.StartsAt < end && start < a.EndsAt);

    private Task<bool> ClientBusyAsync(string clientId, DateTime start, DateTime end, int? exceptId) =>
        db.Appointments.AnyAsync(a => a.ClientId == clientId && a.Id != exceptId
            && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Approved)
            && a.StartsAt < end && start < a.EndsAt);

    private static void SetStatus(Appointment appointment, AppointmentStatus status)
    {
        appointment.Status = status;
        appointment.UpdatedAt = DateTime.UtcNow;
    }

    private async Task<string> MyNameAsync() =>
        IsAdmin ? "An administrator" : await db.Users.Where(u => u.Id == UserId).Select(u => u.FullName).FirstAsync();

    // Tells the client what changed; when an admin made the change, the trainer is told too.
    // message is given "your" for the client and the client's name for the provider.
    private async Task NotifyClientAsync(Appointment a, NotificationType type, string title, Func<string, string> message)
    {
        await notifications.NotifyAsync(a.ClientId, type, title, message("your"), DetailsLink(a));
        if (IsAdmin)
            await notifications.NotifyAsync(a.TrainerId, type, title, message(a.Client!.FullName + "'s"), DetailsLink(a));
    }

    private static string When(Appointment a) => $"{a.StartsAt:ddd d MMM 'at' HH:mm}";

    private static string DetailsLink(Appointment a) => $"/Appointments/Details/{a.Id}";

    private IQueryable<ApplicationUser> Providers() =>
        db.UsersInRole(Roles.Trainer);

    private async Task<List<SelectListItem>> ProviderOptionsAsync(string selected)
    {
        return await db.UsersInRole(Roles.Trainer).OrderBy(u => u.FullName)
            .Select(u => new SelectListItem(u.TrainerProfile != null ? u.FullName + " — " + u.TrainerProfile.Specialization : u.FullName,
                u.Id, u.Id == selected)).ToListAsync();
    }

    private IActionResult BackTo(string? returnUrl, int id) =>
        Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(Details), new { id });

    private IQueryable<Appointment> Mine() =>
        IsAdmin ? db.Appointments
        : IsProvider ? db.Appointments.Where(a => a.TrainerId == UserId) : db.Appointments.Where(a => a.ClientId == UserId);

    private Task<Appointment?> FindOwnAsync(int id) =>
        Mine().Include(a => a.Client).Include(a => a.Trainer).FirstOrDefaultAsync(a => a.Id == id);

    private Task<Appointment?> LoadAsync(int id) =>
        Mine().Include(a => a.Client)
            .Include(a => a.Trainer).ThenInclude(t => t!.TrainerProfile)
            .FirstOrDefaultAsync(a => a.Id == id);

    private async Task<AppointmentsPageViewModel> BuildPageAsync(AppointmentRequestViewModel form)
    {
        var now = DateTime.Now;
        var mine = Mine().Include(a => a.Client).Include(a => a.Trainer);

        return new AppointmentsPageViewModel
        {
            IsProvider = IsProvider,
            IsAdmin = IsAdmin,
            Form = form,
            Trainers = IsProvider ? [] : await ProviderOptionsAsync(form.TrainerId),
            Requests = await mine.Where(a => a.Status == AppointmentStatus.Pending && a.StartsAt > now)
                .OrderBy(a => a.StartsAt).ToListAsync(),
            Upcoming = await mine.Where(a => a.Status == AppointmentStatus.Approved && a.StartsAt > now)
                .OrderBy(a => a.StartsAt).ToListAsync(),
            History = await mine
                .Where(a => !((a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Approved) && a.StartsAt > now))
                .OrderByDescending(a => a.StartsAt).Take(TrackingRules.PageSize).ToListAsync()
        };
    }
}
