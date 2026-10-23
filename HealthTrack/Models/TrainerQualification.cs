using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models;

// A trainer can list several qualifications (1 : many from TrainerProfile).
public class TrainerQualification
{
    public int Id { get; set; }

    public int TrainerProfileId { get; set; }
    public TrainerProfile? TrainerProfile { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}
