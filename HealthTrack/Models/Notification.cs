using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models;

public class Notification
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public NotificationType Type { get; set; }

    [Required, MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string Message { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Link { get; set; }

    // Set for reminders so the same reminder is only created once.
    [MaxLength(100)]
    public string? Key { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}
