using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.ViewModels;

public class ClientProfileFormViewModel
{
    [Required(ErrorMessage = "Please choose your main goal.")]
    [Display(Name = "Main goal")]
    public string? FitnessGoal { get; set; }

    [Required(ErrorMessage = "Please choose how active you are.")]
    [Display(Name = "How active are you day to day?")]
    public string? ActivityLevel { get; set; }

    [StringLength(500)]
    [Display(Name = "Injuries, health conditions or allergies (optional)")]
    public string? HealthConditions { get; set; }
}

public class TrainerProfileFormViewModel
{
    [Required(ErrorMessage = "Please choose your specialization.")]
    [Display(Name = "Specialization")]
    public string? Specialization { get; set; }

    [Required(ErrorMessage = "Enter your experience.")]
    [Range(0, 60, ErrorMessage = "Enter 0 to 60 years.")]
    [Display(Name = "Experience")]
    public int? YearsOfExperience { get; set; }

    // One box per qualification; empty boxes are ignored. Checked in RoleProfiles.Validate.
    [Display(Name = "Qualifications or certifications")]
    public List<string?> Qualifications { get; set; } = [null];
}

public class ProfessionalProfileFormViewModel
{
    [Required(ErrorMessage = "Please choose your profession.")]
    [Display(Name = "Profession")]
    public string? Profession { get; set; }

    [Required(ErrorMessage = "Enter your registration number.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Use 3 to 50 characters.")]
    [Display(Name = "Registration / licence no.")]
    public string? RegistrationNumber { get; set; }

    [StringLength(100)]
    [Display(Name = "Workplace (optional)")]
    public string? Workplace { get; set; }

    [Required(ErrorMessage = "Enter your experience.")]
    [Range(0, 60, ErrorMessage = "Enter 0 to 60 years.")]
    [Display(Name = "Experience")]
    public int? YearsOfExperience { get; set; }
}
