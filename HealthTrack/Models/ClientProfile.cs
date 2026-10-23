using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models;

// One per client (1 : 1 with ApplicationUser), filled in at registration.
public class ClientProfile
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [Required, MaxLength(50)]
    public string FitnessGoal { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string ActivityLevel { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? HealthConditions { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
