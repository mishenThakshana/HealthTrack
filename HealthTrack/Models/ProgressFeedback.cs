using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models;

public class ProgressFeedback
{
    public int Id { get; set; }

    [Required]
    public string ClientId { get; set; } = string.Empty;
    public ApplicationUser? Client { get; set; }

    [Required]
    public string AuthorId { get; set; } = string.Empty;
    public ApplicationUser? Author { get; set; }

    [Required, MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
