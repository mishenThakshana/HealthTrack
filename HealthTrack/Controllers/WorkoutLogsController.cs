using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers;

[Authorize(Roles = Roles.Client)]
public class WorkoutLogsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager) : Controller
{
    private string UserId => userManager.GetUserId(User)!;

    public async Task<IActionResult> Index(int? edit)
    {
        if (edit is null)
            return View(await BuildPageAsync(new WorkoutLogFormViewModel()));

        var log = await FindAsync(edit.Value);
        if (log is null)
            return NotFound();

        return View(await BuildPageAsync(new WorkoutLogFormViewModel
        {
            Date = log.Date,
            Activity = log.Activity,
            DurationMinutes = log.DurationMinutes,
            CaloriesBurned = log.CaloriesBurned,
            Notes = log.Notes
        }, log.Id));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind(Prefix = "Form")] WorkoutLogFormViewModel form)
    {
        TrackingRules.ValidateDate(ModelState, form.Date);
        if (!ModelState.IsValid)
            return View(nameof(Index), await BuildPageAsync(form));

        db.WorkoutLogs.Add(new WorkoutLog
        {
            ClientId = UserId,
            Date = form.Date,
            Activity = form.Activity.Trim(),
            DurationMinutes = form.DurationMinutes!.Value,
            CaloriesBurned = form.CaloriesBurned,
            Notes = TrackingRules.Clean(form.Notes)
        });
        await db.SaveChangesAsync();

        TempData["Success"] = $"Logged {form.Activity.Trim()}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind(Prefix = "Form")] WorkoutLogFormViewModel form)
    {
        var log = await FindAsync(id);
        if (log is null)
            return NotFound();

        TrackingRules.ValidateDate(ModelState, form.Date, log.Date);
        if (!ModelState.IsValid)
            return View(nameof(Index), await BuildPageAsync(form, id));

        log.Date = form.Date;
        log.Activity = form.Activity.Trim();
        log.DurationMinutes = form.DurationMinutes!.Value;
        log.CaloriesBurned = form.CaloriesBurned;
        log.Notes = TrackingRules.Clean(form.Notes);
        await db.SaveChangesAsync();

        TempData["Success"] = $"Updated {log.Activity}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var log = await FindAsync(id);
        if (log is null)
            return NotFound();

        db.WorkoutLogs.Remove(log);
        await db.SaveChangesAsync();

        TempData["Success"] = "Workout entry deleted.";
        return RedirectToAction(nameof(Index));
    }

    private Task<WorkoutLog?> FindAsync(int id) => db.WorkoutLogs.FirstOrDefaultAsync(l => l.Id == id && l.ClientId == UserId);

    private async Task<TrackingPageViewModel<WorkoutLogFormViewModel, WorkoutLog>> BuildPageAsync(WorkoutLogFormViewModel form, int? editingId = null) => new()
    {
        Form = form,
        EditingId = editingId,
        Entries = await db.WorkoutLogs.Where(l => l.ClientId == UserId)
            .OrderByDescending(l => l.Date).ThenByDescending(l => l.Id)
            .Take(TrackingRules.PageSize).ToListAsync()
    };
}
