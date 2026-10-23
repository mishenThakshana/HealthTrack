using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models;

public class Appointment
{
    public int Id { get; set; }

    [Required]
    public string ClientId { get; set; } = string.Empty;
    public ApplicationUser? Client { get; set; }

    [Required]
    public string TrainerId { get; set; } = string.Empty;
    public ApplicationUser? Trainer { get; set; }

    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }

    public AppointmentMode Mode { get; set; }

    [MaxLength(200)]
    public string? Location { get; set; }

    [Required, MaxLength(300)]
    public string Reason { get; set; } = string.Empty;

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;

    [MaxLength(300)]
    public string? TrainerNote { get; set; }

    public bool CancelledByClient { get; set; }
    public bool CancelledByAdmin { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public int DurationMinutes => (int)(EndsAt - StartsAt).TotalMinutes;
}
