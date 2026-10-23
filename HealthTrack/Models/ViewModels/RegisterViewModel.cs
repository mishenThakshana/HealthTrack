using System.ComponentModel.DataAnnotations;
using HealthTrack.Helpers;

namespace HealthTrack.Models.ViewModels;

public class RegisterViewModel
{
    [Required, StringLength(100, MinimumLength = 2)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    [Display(Name = "I am a")]
    public string Role { get; set; } = Data.Roles.Client;

    // Only asked of clients.
    [Required(ErrorMessage = "Please enter your date of birth.")]
    [DataType(DataType.Date)]
    [Display(Name = "Date of birth")]
    public DateOnly? DateOfBirth { get; set; }

    public TrainerProfileFormViewModel Trainer { get; set; } = new();
    public ProfessionalProfileFormViewModel Professional { get; set; } = new();

    [Required(ErrorMessage = "Please select your gender.")]
    [StringLength(20)]
    public string? Gender { get; set; }

    [Required(ErrorMessage = "Please accept the privacy policy to create an account.")]
    [MustBeTrue(ErrorMessage = "Please accept the privacy policy to create an account.")]
    [Display(Name = "I have read and accept the privacy policy")]
    public bool AcceptPrivacy { get; set; }
}
