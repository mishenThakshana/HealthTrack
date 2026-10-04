using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models;

public class MealLog
{
    public int Id { get; set; }

    [Required]
    public string ClientId { get; set; } = string.Empty;
    public ApplicationUser? Client { get; set; }

    public DateOnly Date { get; set; }
    public MealType MealType { get; set; }

    [Required, MaxLength(300)]
    public string Description { get; set; } = string.Empty;

    public int? Calories { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
