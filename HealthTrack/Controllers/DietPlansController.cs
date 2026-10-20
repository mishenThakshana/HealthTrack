using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Models.ViewModels;
using HealthTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers;

[Authorize(Roles = Roles.Client + "," + Roles.Trainer)]
public class DietPlansController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, NotificationService notifications) : Controller
{
    private string UserId => userManager.GetUserId(User)!;
    private bool IsTrainer => User.IsInRole(Roles.Trainer);

    public async Task<IActionResult> Index(string? clientId)
    {
        var plans = db.DietPlans.AsQueryable();
        plans = IsTrainer ? plans.Where(p => p.TrainerId == UserId) : plans.Where(p => p.ClientId == UserId);
        if (IsTrainer && !string.IsNullOrEmpty(clientId))
            plans = plans.Where(p => p.ClientId == clientId);

        return View(new PlanListViewModel
        {
            IsTrainer = IsTrainer,
            ClientFilter = clientId,
            Clients = IsTrainer ? await PlanClients.SelectListAsync(db, UserId, clientId) : [],
            Plans = await plans
                .OrderByDescending(p => p.IsActive).ThenByDescending(p => p.StartDate)
                .Select(p => new PlanListItem(p.Id, p.Title, p.Client!.FullName, p.Trainer!.FullName,
                    p.StartDate, p.EndDate, p.IsActive, p.Meals.Count))
                .ToListAsync()
        });
    }

    public async Task<IActionResult> Details(int id)
    {
        var plan = await LoadPlanAsync(id);
        if (plan is null || (plan.TrainerId != UserId && plan.ClientId != UserId))
            return NotFound();

        return View(new DietPlanDetailsViewModel { Plan = plan, CanEdit = plan.TrainerId == UserId });
    }

    [Authorize(Roles = Roles.Trainer)]
    public async Task<IActionResult> Create(string? clientId)
    {
        var model = new PlanFormViewModel { ClientId = clientId ?? string.Empty };
        model.Clients = await PlanClients.SelectListAsync(db, UserId, model.ClientId);
        return View("PlanForm", model);
    }

    [HttpPost, Authorize(Roles = Roles.Trainer), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PlanFormViewModel model)
    {
        await PlanClients.ValidateAsync(db, ModelState, model, UserId, null);
        if (!ModelState.IsValid)
        {
            model.Clients = await PlanClients.SelectListAsync(db, UserId, model.ClientId);
            return View("PlanForm", model);
        }

        var plan = new DietPlan { TrainerId = UserId };
        Apply(model, plan);
        db.DietPlans.Add(plan);
        await db.SaveChangesAsync();
        await NotifyClientAsync(plan);

        TempData["Success"] = "Diet plan created. Now add some meals.";
        return RedirectToAction(nameof(Details), new { id = plan.Id });
    }

    [Authorize(Roles = Roles.Trainer)]
    public async Task<IActionResult> Edit(int id)
    {
        var plan = await db.DietPlans.FirstOrDefaultAsync(p => p.Id == id && p.TrainerId == UserId);
        if (plan is null)
            return NotFound();

        var model = new PlanFormViewModel
        {
            Id = plan.Id,
            Title = plan.Title,
            Description = plan.Description,
            ClientId = plan.ClientId,
            StartDate = plan.StartDate,
            EndDate = plan.EndDate,
            IsActive = plan.IsActive,
            DailyCalorieTarget = plan.DailyCalorieTarget
        };
        model.Clients = await PlanClients.SelectListAsync(db, UserId, model.ClientId, plan.ClientId);
        return View("PlanForm", model);
    }

    [HttpPost, Authorize(Roles = Roles.Trainer), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PlanFormViewModel model)
    {
        var plan = await db.DietPlans.FirstOrDefaultAsync(p => p.Id == id && p.TrainerId == UserId);
        if (plan is null)
            return NotFound();

        model.Id = id;
        await PlanClients.ValidateAsync(db, ModelState, model, UserId, plan.ClientId);
        if (!ModelState.IsValid)
        {
            model.Clients = await PlanClients.SelectListAsync(db, UserId, model.ClientId, plan.ClientId);
            return View("PlanForm", model);
        }

        var previousClient = plan.ClientId;
        Apply(model, plan);
        await db.SaveChangesAsync();
        if (plan.ClientId != previousClient)
            await NotifyClientAsync(plan);

        TempData["Success"] = "Diet plan updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, Authorize(Roles = Roles.Trainer), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var plan = await db.DietPlans.FirstOrDefaultAsync(p => p.Id == id && p.TrainerId == UserId);
        if (plan is null)
            return NotFound();

        db.DietPlans.Remove(plan);
        await db.SaveChangesAsync();

        TempData["Success"] = $"Deleted \"{plan.Title}\".";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, Authorize(Roles = Roles.Trainer), ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMeal(int id, [Bind(Prefix = "NewMeal")] MealFormViewModel form)
    {
        var plan = await LoadPlanAsync(id);
        if (plan is null || plan.TrainerId != UserId)
            return NotFound();

        if (!Enum.IsDefined(form.MealType))
            ModelState.AddModelError("NewMeal.MealType", "Please choose a meal.");

        if (!ModelState.IsValid)
            return View(nameof(Details), new DietPlanDetailsViewModel { Plan = plan, CanEdit = true, NewMeal = form });

        db.DietMeals.Add(new DietMeal
        {
            DietPlanId = id,
            MealType = form.MealType,
            Description = form.Description.Trim(),
            Calories = form.Calories
        });
        await db.SaveChangesAsync();

        TempData["Success"] = $"Added a {form.MealType.ToString().ToLowerInvariant()} item.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, Authorize(Roles = Roles.Trainer), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMeal(int id, int mealId)
    {
        var meal = await db.DietMeals
            .FirstOrDefaultAsync(m => m.Id == mealId && m.DietPlanId == id && m.DietPlan!.TrainerId == UserId);
        if (meal is null)
            return NotFound();

        db.DietMeals.Remove(meal);
        await db.SaveChangesAsync();

        TempData["Success"] = "Meal removed.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private Task<DietPlan?> LoadPlanAsync(int id) =>
        db.DietPlans
            .Include(p => p.Client)
            .Include(p => p.Trainer)
            .Include(p => p.Meals)
            .FirstOrDefaultAsync(p => p.Id == id);

    private async Task NotifyClientAsync(DietPlan plan)
    {
        var trainer = await db.Users.Where(u => u.Id == UserId).Select(u => u.FullName).FirstAsync();
        await notifications.NotifyAsync(plan.ClientId, NotificationType.PlanAssigned, "New diet plan",
            $"{trainer} created the diet plan “{plan.Title}” for you.", $"/DietPlans/Details/{plan.Id}");
    }

    private static void Apply(PlanFormViewModel model, DietPlan plan)
    {
        plan.Title = model.Title.Trim();
        plan.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        plan.ClientId = model.ClientId;
        plan.StartDate = model.StartDate;
        plan.EndDate = model.EndDate;
        plan.IsActive = model.IsActive;
        plan.DailyCalorieTarget = model.DailyCalorieTarget;
    }
}
