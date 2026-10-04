using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models;

public class HealthMeasurement
{
    public int Id { get; set; }

    [Required]
    public string ClientId { get; set; } = string.Empty;
    public ApplicationUser? Client { get; set; }

    public DateOnly Date { get; set; }
    public decimal WeightKg { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? BodyFatPercent { get; set; }
    public int? RestingHeartRate { get; set; }

    [MaxLength(300)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public decimal? Bmi => HeightCm is > 0 ? Math.Round(WeightKg / (HeightCm.Value / 100m * (HeightCm.Value / 100m)), 1) : null;
}
