using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.ViewModels;

public class WorkoutLogFormViewModel
{
    [Required, DataType(DataType.Date)]
    public DateOnly Date { get; set; } = Helpers.Dates.Today;

    [Required, StringLength(100, MinimumLength = 2)]
    public string Activity { get; set; } = string.Empty;

    [Required, Range(1, 600)]
    [Display(Name = "Duration (minutes)")]
    public int? DurationMinutes { get; set; }

    [Range(0, 5000)]
    [Display(Name = "Calories burned")]
    public int? CaloriesBurned { get; set; }

    [StringLength(300)]
    public string? Notes { get; set; }
}

public class MealLogFormViewModel
{
    [Required, DataType(DataType.Date)]
    public DateOnly Date { get; set; } = Helpers.Dates.Today;

    [Display(Name = "Meal")]
    public MealType MealType { get; set; }

    [Required, StringLength(300, MinimumLength = 2)]
    [Display(Name = "What did you eat?")]
    public string Description { get; set; } = string.Empty;

    [Range(0, 5000)]
    public int? Calories { get; set; }
}

public class MeasurementFormViewModel
{
    [Required, DataType(DataType.Date)]
    public DateOnly Date { get; set; } = Helpers.Dates.Today;

    [Required, Range(20, 400)]
    [Display(Name = "Weight (kg)")]
    public decimal? WeightKg { get; set; }

    [Range(50, 260)]
    [Display(Name = "Height (cm)")]
    public decimal? HeightCm { get; set; }

    [Range(2, 75)]
    [Display(Name = "Body fat (%)")]
    public decimal? BodyFatPercent { get; set; }

    [Range(25, 220)]
    [Display(Name = "Resting heart rate (bpm)")]
    public int? RestingHeartRate { get; set; }

    [StringLength(300)]
    public string? Notes { get; set; }
}

public class TrackingPageViewModel<TForm, TEntry> where TForm : new()
{
    public TForm Form { get; init; } = new();
    public int? EditingId { get; init; }
    public IReadOnlyList<TEntry> Entries { get; init; } = [];
    public PlanScheduleViewModel Plan { get; init; } = new();
}

public record PlannedExercise(WorkoutExercise Exercise, bool Logged);

public record PlannedMeal(DietMeal Meal, bool Logged);

public record UpcomingWorkoutDay(DateOnly Date, WorkoutPlan Plan, IReadOnlyList<WorkoutExercise> Exercises);

public class PlanScheduleViewModel
{
    public WorkoutPlan? WorkoutPlan { get; init; }
    public IReadOnlyList<PlannedExercise> Exercises { get; init; } = [];
    public IReadOnlyList<UpcomingWorkoutDay> UpcomingWorkouts { get; init; } = [];
    public WorkoutPlan? NextWorkoutPlan { get; init; }

    public DietPlan? DietPlan { get; init; }
    public IReadOnlyList<PlannedMeal> Meals { get; init; } = [];
    public DietPlan? NextDietPlan { get; init; }
}
