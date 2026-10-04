using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers;

[Authorize(Roles = Roles.Client)]
public class MealLogsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager) : Controller
{
    private string UserId => userManager.GetUserId(User)!;

    public async Task<IActionResult> Index() => View(await BuildPageAsync(new MealLogFormViewModel()));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind(Prefix = "Form")] MealLogFormViewModel form)
    {
        TrackingRules.ValidateDate(ModelState, form.Date);
        if (!Enum.IsDefined(form.MealType))
            ModelState.AddModelError("Form.MealType", "Please choose a meal.");
        if (!ModelState.IsValid)
            return View(nameof(Index), await BuildPageAsync(form));

        db.MealLogs.Add(new MealLog
        {
            ClientId = UserId,
            Date = form.Date,
            MealType = form.MealType,
            Description = form.Description.Trim(),
            Calories = form.Calories
        });
        await db.SaveChangesAsync();

        TempData["Success"] = $"Logged your {form.MealType.ToString().ToLowerInvariant()}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var log = await db.MealLogs.FirstOrDefaultAsync(l => l.Id == id && l.ClientId == UserId);
        if (log is null)
            return NotFound();

        db.MealLogs.Remove(log);
        await db.SaveChangesAsync();

        TempData["Success"] = "Meal entry deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<TrackingPageViewModel<MealLogFormViewModel, MealLog>> BuildPageAsync(MealLogFormViewModel form) => new()
    {
        Form = form,
        Entries = await db.MealLogs.Where(l => l.ClientId == UserId)
            .OrderByDescending(l => l.Date).ThenByDescending(l => l.Id)
            .Take(TrackingRules.PageSize).ToListAsync()
    };
}
