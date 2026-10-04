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

    public async Task<IActionResult> Index() => View(await BuildPageAsync(new WorkoutLogFormViewModel()));

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
    public async Task<IActionResult> Delete(int id)
    {
        var log = await db.WorkoutLogs.FirstOrDefaultAsync(l => l.Id == id && l.ClientId == UserId);
        if (log is null)
            return NotFound();

        db.WorkoutLogs.Remove(log);
        await db.SaveChangesAsync();

        TempData["Success"] = "Workout entry deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<TrackingPageViewModel<WorkoutLogFormViewModel, WorkoutLog>> BuildPageAsync(WorkoutLogFormViewModel form) => new()
    {
        Form = form,
        Entries = await db.WorkoutLogs.Where(l => l.ClientId == UserId)
            .OrderByDescending(l => l.Date).ThenByDescending(l => l.Id)
            .Take(TrackingRules.PageSize).ToListAsync()
    };
}
