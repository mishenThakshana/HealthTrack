using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models;

// One per health professional (1 : 1 with ApplicationUser), filled in at registration.
public class ProfessionalProfile
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [Required, MaxLength(50)]
    public string Profession { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string RegistrationNumber { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Workplace { get; set; }

    public int YearsOfExperience { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
