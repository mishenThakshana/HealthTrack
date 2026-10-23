using HealthTrack.Data;
using HealthTrack.Helpers;
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

    public async Task<IActionResult> Index(int? edit, int? meal)
    {
        if (meal is not null)
        {
            var planned = await FindPlannedAsync(meal.Value);
            if (planned is null)
                return NotFound();

            return View(await BuildPageAsync(PlanSchedule.LogFormFor(planned.Meal)));
        }

        if (edit is null)
            return View(await BuildPageAsync(new MealLogFormViewModel()));

        var log = await FindAsync(edit.Value);
        if (log is null)
            return NotFound();

        return View(await BuildPageAsync(new MealLogFormViewModel
        {
            Date = log.Date,
            MealType = log.MealType,
            Description = log.Description,
            Calories = log.Calories
        }, log.Id));
    }

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
    public async Task<IActionResult> LogPlanned(int mealId, string? returnUrl)
    {
        var planned = await FindPlannedAsync(mealId);
        if (planned is null)
            return NotFound();

        var meal = planned.Meal;
        var name = meal.MealType.ToString().ToLowerInvariant();
        if (planned.Logged)
        {
            TempData["Success"] = $"Your planned {name} is already logged for today.";
        }
        else
        {
            db.MealLogs.Add(new MealLog
            {
                ClientId = UserId,
                Date = Dates.Today,
                MealType = meal.MealType,
                Description = meal.Description,
                Calories = meal.Calories
            });
            await db.SaveChangesAsync();
            TempData["Success"] = $"Logged your {name}.";
        }

        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind(Prefix = "Form")] MealLogFormViewModel form)
    {
        var log = await FindAsync(id);
        if (log is null)
            return NotFound();

        TrackingRules.ValidateDate(ModelState, form.Date, log.Date);
        if (!Enum.IsDefined(form.MealType))
            ModelState.AddModelError("Form.MealType", "Please choose a meal.");
        if (!ModelState.IsValid)
            return View(nameof(Index), await BuildPageAsync(form, id));

        log.Date = form.Date;
        log.MealType = form.MealType;
        log.Description = form.Description.Trim();
        log.Calories = form.Calories;
        await db.SaveChangesAsync();

        TempData["Success"] = "Meal entry updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var log = await FindAsync(id);
        if (log is null)
            return NotFound();

        db.MealLogs.Remove(log);
        await db.SaveChangesAsync();

        TempData["Success"] = "Meal entry deleted.";
        return RedirectToAction(nameof(Index));
    }

    private Task<MealLog?> FindAsync(int id) => db.MealLogs.FirstOrDefaultAsync(l => l.Id == id && l.ClientId == UserId);

    private async Task<PlannedMeal?> FindPlannedAsync(int mealId) =>
        (await PlanSchedule.ForClientAsync(db, UserId)).Meals.FirstOrDefault(p => p.Meal.Id == mealId);

    private async Task<TrackingPageViewModel<MealLogFormViewModel, MealLog>> BuildPageAsync(MealLogFormViewModel form, int? editingId = null) => new()
    {
        Form = form,
        EditingId = editingId,
        Entries = await db.MealLogs.Where(l => l.ClientId == UserId)
            .OrderByDescending(l => l.Date).ThenByDescending(l => l.Id)
            .Take(TrackingRules.PageSize).ToListAsync(),
        Plan = await PlanSchedule.ForClientAsync(db, UserId)
    };
}
