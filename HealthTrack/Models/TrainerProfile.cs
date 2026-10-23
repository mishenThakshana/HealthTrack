using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models;

// One per trainer (1 : 1 with ApplicationUser), filled in at registration.
public class TrainerProfile
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [Required, MaxLength(50)]
    public string Specialization { get; set; } = string.Empty;

    public int YearsOfExperience { get; set; }

    public List<TrainerQualification> Qualifications { get; set; } = [];

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
