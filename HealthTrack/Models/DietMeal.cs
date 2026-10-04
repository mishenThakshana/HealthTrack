using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models;

public class DietMeal
{
    public int Id { get; set; }

    public int DietPlanId { get; set; }
    public DietPlan? DietPlan { get; set; }

    public MealType MealType { get; set; }

    [Required, MaxLength(300)]
    public string Description { get; set; } = string.Empty;

    public int? Calories { get; set; }
}
