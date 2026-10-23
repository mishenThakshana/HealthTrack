using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace HealthTrack.Models;

public class ApplicationUser : IdentityUser
{
    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    public DateOnly? DateOfBirth { get; set; }

    [MaxLength(20)]
    public string? Gender { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public DateTime? PrivacyAcceptedAt { get; set; }

    public bool EmailAppointments { get; set; } = true;
    public bool EmailReminders { get; set; } = true;
    public bool EmailUpdates { get; set; } = true;
    public bool RemindWorkouts { get; set; } = true;
    public bool RemindMeals { get; set; } = true;

    public string? TrainerId { get; set; }
    public ApplicationUser? Trainer { get; set; }

    // When the profile picture last changed; null when the user has none. Also used to refresh cached copies.
    public DateTime? PhotoUpdatedAt { get; set; }

    public ClientProfile? ClientProfile { get; set; }
    public TrainerProfile? TrainerProfile { get; set; }
}
