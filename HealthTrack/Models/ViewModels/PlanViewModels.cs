using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HealthTrack.Models.ViewModels;

public class PlanFormViewModel
{
    public int? Id { get; set; }

    [Required, StringLength(100, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Please choose a client.")]
    [Display(Name = "Client")]
    public string ClientId { get; set; } = string.Empty;

    [Required, DataType(DataType.Date)]
    [Display(Name = "Start date")]
    public DateOnly StartDate { get; set; } = Helpers.Dates.Today;

    [DataType(DataType.Date)]
    [Display(Name = "End date")]
    public DateOnly? EndDate { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    [Range(500, 10000)]
    [Display(Name = "Daily calorie target")]
    public int? DailyCalorieTarget { get; set; }

    public IEnumerable<SelectListItem> Clients { get; set; } = [];
}

public class ExerciseFormViewModel
{
    public DayOfWeek? Day { get; set; }

    [Required, StringLength(100, MinimumLength = 2)]
    [Display(Name = "Exercise")]
    public string Name { get; set; } = string.Empty;

    [Range(1, 20)]
    public int? Sets { get; set; }

    [Range(1, 200)]
    public int? Reps { get; set; }

    [Range(1, 600)]
    [Display(Name = "Minutes")]
    public int? DurationMinutes { get; set; }

    [StringLength(300)]
    public string? Notes { get; set; }
}

public class MealFormViewModel
{
    [Display(Name = "Meal")]
    public MealType MealType { get; set; }

    [Required, StringLength(300, MinimumLength = 2)]
    [Display(Name = "What to eat")]
    public string Description { get; set; } = string.Empty;

    [Range(0, 5000)]
    public int? Calories { get; set; }
}

public class WorkoutPlanDetailsViewModel
{
    public required WorkoutPlan Plan { get; init; }
    public bool CanEdit { get; init; }
    public ExerciseFormViewModel NewExercise { get; init; } = new();
}

public class DietPlanDetailsViewModel
{
    public required DietPlan Plan { get; init; }
    public bool CanEdit { get; init; }
    public MealFormViewModel NewMeal { get; init; } = new();
}

public record PlanListItem(
    int Id, string Title, string ClientName, string TrainerName,
    DateOnly StartDate, DateOnly? EndDate, bool IsActive, int ItemCount);

public class PlanListViewModel
{
    public IReadOnlyList<PlanListItem> Plans { get; init; } = [];
    public bool IsTrainer { get; init; }
    public string? ClientFilter { get; init; }
    public IEnumerable<SelectListItem> Clients { get; init; } = [];
}

public record PlanHeader(
    int Id, string ListTitle, string Title, string? Description, string ClientName, string TrainerName,
    DateOnly StartDate, DateOnly? EndDate, bool IsActive, bool CanEdit);
