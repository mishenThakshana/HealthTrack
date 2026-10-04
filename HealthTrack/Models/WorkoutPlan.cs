using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models;

public class WorkoutPlan
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public string ClientId { get; set; } = string.Empty;
    public ApplicationUser? Client { get; set; }

    [Required]
    public string TrainerId { get; set; } = string.Empty;
    public ApplicationUser? Trainer { get; set; }

    public List<WorkoutExercise> Exercises { get; set; } = [];
}
