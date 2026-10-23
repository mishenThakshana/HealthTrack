using HealthTrack.Data;
using HealthTrack.Helpers;
using HealthTrack.Models;
using HealthTrack.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers;

/// <summary>What a client's active plans assign today and over the coming days, and what they've already logged today.</summary>
internal static class PlanSchedule
{
    public const int UpcomingDays = 6;

    public static async Task<PlanScheduleViewModel> ForClientAsync(ApplicationDbContext db, string clientId)
    {
        var today = Dates.Today;

        var workoutPlans = await db.WorkoutPlans.Include(p => p.Exercises)
            .Where(p => p.ClientId == clientId && p.IsActive && (p.EndDate == null || p.EndDate >= today))
            .ToListAsync();
        var dietPlans = await db.DietPlans.Include(p => p.Meals)
            .Where(p => p.ClientId == clientId && p.IsActive && (p.EndDate == null || p.EndDate >= today))
            .ToListAsync();

        var workoutLogs = await db.WorkoutLogs.Where(l => l.ClientId == clientId && l.Date == today)
            .Select(l => l.Activity).ToListAsync();
        var mealLogs = await db.MealLogs.Where(l => l.ClientId == clientId && l.Date == today)
            .Select(l => new { l.MealType, l.Description }).ToListAsync();

        var workoutPlan = WorkoutPlanOn(workoutPlans, today);
        var dietPlan = DietPlanOn(dietPlans, today);

        return new PlanScheduleViewModel
        {
            WorkoutPlan = workoutPlan,
            Exercises = workoutPlan is null ? [] : ExercisesOn(workoutPlan, today)
                .Select(e => new PlannedExercise(e, workoutLogs.Contains(e.Name, StringComparer.OrdinalIgnoreCase)))
                .ToList(),
            UpcomingWorkouts = Enumerable.Range(1, UpcomingDays)
                .Select(i => today.AddDays(i))
                .Select(date => (Date: date, Plan: WorkoutPlanOn(workoutPlans, date)))
                .Where(d => d.Plan is not null)
                .Select(d => new UpcomingWorkoutDay(d.Date, d.Plan!, ExercisesOn(d.Plan!, d.Date)))
                .ToList(),
            NextWorkoutPlan = workoutPlans.Where(p => p.StartDate > today).MinBy(p => p.StartDate),
            DietPlan = dietPlan,
            Meals = dietPlan is null ? [] : dietPlan.Meals.OrderBy(m => m.MealType).ThenBy(m => m.Id)
                .Select(m => new PlannedMeal(m, mealLogs.Any(l => l.MealType == m.MealType
                    && string.Equals(l.Description, m.Description, StringComparison.OrdinalIgnoreCase))))
                .ToList(),
            NextDietPlan = dietPlans.Where(p => p.StartDate > today).MinBy(p => p.StartDate)
        };
    }

    public static WorkoutLogFormViewModel LogFormFor(WorkoutExercise exercise) => new()
    {
        Activity = exercise.Name,
        DurationMinutes = exercise.DurationMinutes,
        Notes = SetsAndReps(exercise)
    };

    public static MealLogFormViewModel LogFormFor(DietMeal meal) => new()
    {
        MealType = meal.MealType,
        Description = meal.Description,
        Calories = meal.Calories
    };

    public static string? SetsAndReps(WorkoutExercise exercise) =>
        exercise.Sets is null && exercise.Reps is null ? null : $"{exercise.Sets ?? 1} × {exercise.Reps?.ToString() ?? "–"} reps";

    private static List<WorkoutExercise> ExercisesOn(WorkoutPlan plan, DateOnly date) =>
        plan.Exercises.Where(e => e.Day is null || e.Day == date.DayOfWeek).OrderBy(e => e.Day is null).ThenBy(e => e.Id).ToList();

    private static WorkoutPlan? WorkoutPlanOn(IEnumerable<WorkoutPlan> plans, DateOnly date) =>
        plans.Where(p => p.StartDate <= date && (p.EndDate == null || p.EndDate >= date)).MaxBy(p => p.StartDate);

    private static DietPlan? DietPlanOn(IEnumerable<DietPlan> plans, DateOnly date) =>
        plans.Where(p => p.StartDate <= date && (p.EndDate == null || p.EndDate >= date)).MaxBy(p => p.StartDate);
}
