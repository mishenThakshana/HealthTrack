using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models;

public class WorkoutLog
{
    public int Id { get; set; }

    [Required]
    public string ClientId { get; set; } = string.Empty;
    public ApplicationUser? Client { get; set; }

    public DateOnly Date { get; set; }

    [Required, MaxLength(100)]
    public string Activity { get; set; } = string.Empty;

    public int DurationMinutes { get; set; }
    public int? CaloriesBurned { get; set; }

    [MaxLength(300)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
