using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers;

[Authorize(Roles = Roles.Client + "," + Roles.Trainer)]
public class WorkoutPlansController(ApplicationDbContext db, UserManager<ApplicationUser> userManager) : Controller
{
    private string UserId => userManager.GetUserId(User)!;
    private bool IsTrainer => User.IsInRole(Roles.Trainer);

    public async Task<IActionResult> Index(string? clientId)
    {
        var plans = db.WorkoutPlans.AsQueryable();
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
                    p.StartDate, p.EndDate, p.IsActive, p.Exercises.Count))
                .ToListAsync()
        });
    }

    public async Task<IActionResult> Details(int id)
    {
        var plan = await LoadPlanAsync(id);
        if (plan is null || (plan.TrainerId != UserId && plan.ClientId != UserId))
            return NotFound();

        return View(new WorkoutPlanDetailsViewModel { Plan = plan, CanEdit = plan.TrainerId == UserId });
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

        var plan = new WorkoutPlan { TrainerId = UserId };
        Apply(model, plan);
        db.WorkoutPlans.Add(plan);
        await db.SaveChangesAsync();

        TempData["Success"] = "Workout plan created. Now add some exercises.";
        return RedirectToAction(nameof(Details), new { id = plan.Id });
    }

    [Authorize(Roles = Roles.Trainer)]
    public async Task<IActionResult> Edit(int id)
    {
        var plan = await db.WorkoutPlans.FirstOrDefaultAsync(p => p.Id == id && p.TrainerId == UserId);
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
            IsActive = plan.IsActive
        };
        model.Clients = await PlanClients.SelectListAsync(db, UserId, model.ClientId, plan.ClientId);
        return View("PlanForm", model);
    }

    [HttpPost, Authorize(Roles = Roles.Trainer), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PlanFormViewModel model)
    {
        var plan = await db.WorkoutPlans.FirstOrDefaultAsync(p => p.Id == id && p.TrainerId == UserId);
        if (plan is null)
            return NotFound();

        model.Id = id;
        await PlanClients.ValidateAsync(db, ModelState, model, UserId, plan.ClientId);
        if (!ModelState.IsValid)
        {
            model.Clients = await PlanClients.SelectListAsync(db, UserId, model.ClientId, plan.ClientId);
            return View("PlanForm", model);
        }

        Apply(model, plan);
        await db.SaveChangesAsync();

        TempData["Success"] = "Workout plan updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, Authorize(Roles = Roles.Trainer), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var plan = await db.WorkoutPlans.FirstOrDefaultAsync(p => p.Id == id && p.TrainerId == UserId);
        if (plan is null)
            return NotFound();

        db.WorkoutPlans.Remove(plan);
        await db.SaveChangesAsync();

        TempData["Success"] = $"Deleted \"{plan.Title}\".";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, Authorize(Roles = Roles.Trainer), ValidateAntiForgeryToken]
    public async Task<IActionResult> AddExercise(int id, [Bind(Prefix = "NewExercise")] ExerciseFormViewModel form)
    {
        var plan = await LoadPlanAsync(id);
        if (plan is null || plan.TrainerId != UserId)
            return NotFound();

        if (form.Sets is null && form.Reps is null && form.DurationMinutes is null)
            ModelState.AddModelError("NewExercise.Sets", "Enter sets and reps, or a duration.");

        if (!ModelState.IsValid)
            return View(nameof(Details), new WorkoutPlanDetailsViewModel { Plan = plan, CanEdit = true, NewExercise = form });

        db.WorkoutExercises.Add(new WorkoutExercise
        {
            WorkoutPlanId = id,
            Day = form.Day,
            Name = form.Name.Trim(),
            Sets = form.Sets,
            Reps = form.Reps,
            DurationMinutes = form.DurationMinutes,
            Notes = form.Notes?.Trim()
        });
        await db.SaveChangesAsync();

        TempData["Success"] = $"Added {form.Name.Trim()}.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, Authorize(Roles = Roles.Trainer), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteExercise(int id, int exerciseId)
    {
        var exercise = await db.WorkoutExercises
            .FirstOrDefaultAsync(e => e.Id == exerciseId && e.WorkoutPlanId == id && e.WorkoutPlan!.TrainerId == UserId);
        if (exercise is null)
            return NotFound();

        db.WorkoutExercises.Remove(exercise);
        await db.SaveChangesAsync();

        TempData["Success"] = $"Removed {exercise.Name}.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private Task<WorkoutPlan?> LoadPlanAsync(int id) =>
        db.WorkoutPlans
            .Include(p => p.Client)
            .Include(p => p.Trainer)
            .Include(p => p.Exercises)
            .FirstOrDefaultAsync(p => p.Id == id);

    private static void Apply(PlanFormViewModel model, WorkoutPlan plan)
    {
        plan.Title = model.Title.Trim();
        plan.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        plan.ClientId = model.ClientId;
        plan.StartDate = model.StartDate;
        plan.EndDate = model.EndDate;
        plan.IsActive = model.IsActive;
    }
}
