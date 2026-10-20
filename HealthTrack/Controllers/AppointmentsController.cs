using HealthTrack.Data;
using HealthTrack.Helpers;
using HealthTrack.Models;
using HealthTrack.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers;

[Authorize(Roles = Roles.Client + "," + Roles.Trainer)]
public class AppointmentsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager) : Controller
{
    private string UserId => userManager.GetUserId(User)!;
    private bool IsTrainer => User.IsInRole(Roles.Trainer);

    public async Task<IActionResult> Index()
    {
        var form = new AppointmentRequestViewModel();
        if (!IsTrainer)
            form.TrainerId = await db.Users.Where(u => u.Id == UserId).Select(u => u.TrainerId).FirstOrDefaultAsync() ?? string.Empty;

        return View(await BuildPageAsync(form));
    }

    public async Task<IActionResult> Details(int id)
    {
        var appointment = await LoadAsync(id);
        if (appointment is null)
            return NotFound();

        return View(new AppointmentDetailsViewModel { Appointment = appointment, IsTrainer = IsTrainer });
    }

    [HttpPost, Authorize(Roles = Roles.Client), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind(Prefix = "Form")] AppointmentRequestViewModel form)
    {
        var trainerName = await db.UsersInRole(Roles.Trainer)
            .Where(u => u.Id == form.TrainerId).Select(u => u.FullName).FirstOrDefaultAsync();
        if (!string.IsNullOrEmpty(form.TrainerId) && trainerName is null)
            ModelState.AddModelError("Form.TrainerId", "Please choose a trainer.");

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

        db.Appointments.Add(new Appointment
        {
            ClientId = UserId,
            TrainerId = form.TrainerId,
            StartsAt = form.StartsAt,
            EndsAt = form.EndsAt,
            Mode = form.Mode,
            Reason = form.Reason.Trim()
        });
        await db.SaveChangesAsync();

        TempData["Success"] = $"Request sent to {trainerName}. You'll see it under Upcoming once it's approved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, Authorize(Roles = Roles.Trainer), ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string? returnUrl)
    {
        var appointment = await FindOwnAsync(id);
        if (appointment is null || !AppointmentRules.CanRespond(appointment))
            return NotFound();

        if (await TrainerBusyAsync(UserId, appointment.StartsAt, appointment.EndsAt, id))
        {
            TempData["Error"] = "You already have an appointment at that time. Reschedule this request or decline it.";
            return RedirectToAction(nameof(Details), new { id });
        }

        SetStatus(appointment, AppointmentStatus.Approved);
        await db.SaveChangesAsync();

        TempData["Success"] = $"Approved {appointment.Client!.FullName}'s appointment on {appointment.StartsAt:ddd d MMM 'at' HH:mm}.";
        return BackTo(returnUrl, id);
    }

    [HttpPost, Authorize(Roles = Roles.Trainer), ValidateAntiForgeryToken]
    public async Task<IActionResult> Decline(int id, string? note, string? returnUrl)
    {
        var appointment = await FindOwnAsync(id);
        if (appointment is null || !AppointmentRules.CanRespond(appointment))
            return NotFound();

        SetStatus(appointment, AppointmentStatus.Declined);
        appointment.TrainerNote = TrackingRules.Clean(note) ?? appointment.TrainerNote;
        await db.SaveChangesAsync();

        TempData["Success"] = $"Declined {appointment.Client!.FullName}'s request.";
        return BackTo(returnUrl, id);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? note)
    {
        var appointment = await FindOwnAsync(id);
        if (appointment is null || !AppointmentRules.CanCancel(appointment, IsTrainer))
            return NotFound();

        SetStatus(appointment, AppointmentStatus.Cancelled);
        appointment.CancelledByClient = !IsTrainer;
        if (IsTrainer)
            appointment.TrainerNote = TrackingRules.Clean(note) ?? appointment.TrainerNote;
        await db.SaveChangesAsync();

        TempData["Success"] = "Appointment cancelled.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, Authorize(Roles = Roles.Trainer), ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id, string? returnUrl)
    {
        var appointment = await FindOwnAsync(id);
        if (appointment is null || !AppointmentRules.CanComplete(appointment))
            return NotFound();

        SetStatus(appointment, AppointmentStatus.Completed);
        await db.SaveChangesAsync();

        TempData["Success"] = "Marked as completed.";
        return BackTo(returnUrl, id);
    }

    [Authorize(Roles = Roles.Trainer)]
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

    [HttpPost, Authorize(Roles = Roles.Trainer), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind(Prefix = "Form")] AppointmentEditViewModel form)
    {
        var appointment = await FindOwnAsync(id);
        if (appointment is null || !AppointmentRules.CanReschedule(appointment))
            return NotFound();

        ValidateSlot(ModelState, "Form", form);
        if (ModelState.IsValid)
        {
            if (await TrainerBusyAsync(UserId, form.StartsAt, form.EndsAt, id))
                ModelState.AddModelError("Form.Time", "You already have an appointment at that time.");
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

    private IActionResult BackTo(string? returnUrl, int id) =>
        Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(Details), new { id });

    private IQueryable<Appointment> Mine() =>
        IsTrainer ? db.Appointments.Where(a => a.TrainerId == UserId) : db.Appointments.Where(a => a.ClientId == UserId);

    private Task<Appointment?> FindOwnAsync(int id) => Mine().Include(a => a.Client).FirstOrDefaultAsync(a => a.Id == id);

    private Task<Appointment?> LoadAsync(int id) =>
        Mine().Include(a => a.Client).Include(a => a.Trainer).FirstOrDefaultAsync(a => a.Id == id);

    private async Task<AppointmentsPageViewModel> BuildPageAsync(AppointmentRequestViewModel form)
    {
        var now = DateTime.Now;
        var mine = Mine().Include(a => a.Client).Include(a => a.Trainer);

        return new AppointmentsPageViewModel
        {
            IsTrainer = IsTrainer,
            Form = form,
            Trainers = IsTrainer ? [] : await db.UsersInRole(Roles.Trainer)
                .OrderBy(u => u.FullName)
                .Select(u => new SelectListItem(u.FullName, u.Id, u.Id == form.TrainerId))
                .ToListAsync(),
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
