using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models;

// A client's star rating and comment for a completed appointment (at most one per appointment).
public class AppointmentReview
{
    public int Id { get; set; }

    public int AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }

    [Required]
    public string ClientId { get; set; } = string.Empty;
    public ApplicationUser? Client { get; set; }

    // The trainer the appointment was with.
    [Required]
    public string ProviderId { get; set; } = string.Empty;
    public ApplicationUser? Provider { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; }

    [MaxLength(1000)]
    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
