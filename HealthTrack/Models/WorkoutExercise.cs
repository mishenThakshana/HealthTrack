using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models;

public class WorkoutExercise
{
    public int Id { get; set; }

    public int WorkoutPlanId { get; set; }
    public WorkoutPlan? WorkoutPlan { get; set; }

    public DayOfWeek? Day { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public int? Sets { get; set; }
    public int? Reps { get; set; }
    public int? DurationMinutes { get; set; }

    [MaxLength(300)]
    public string? Notes { get; set; }
}
