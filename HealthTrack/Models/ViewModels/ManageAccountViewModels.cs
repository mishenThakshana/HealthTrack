using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.ViewModels;

public class ProfileFormViewModel
{
    [Required, StringLength(100, MinimumLength = 2)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    [Display(Name = "Date of birth")]
    public DateOnly? DateOfBirth { get; set; }

    [Required(ErrorMessage = "Please select your gender.")]
    [StringLength(20)]
    public string? Gender { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Current password")]
    public string? CurrentPassword { get; set; }
}

public class ChangePasswordViewModel
{
    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Current password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8)]
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm new password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ManageAccountViewModel
{
    public ProfileFormViewModel Profile { get; init; } = new();
    public ChangePasswordViewModel Password { get; init; } = new();
    public NotificationSettingsViewModel Notifications { get; init; } = new();
    public ClientProfileFormViewModel Client { get; init; } = new();
    public TrainerProfileFormViewModel Trainer { get; init; } = new();
    public ProfessionalProfileFormViewModel Professional { get; init; } = new();
    public bool EmailConfirmed { get; init; }
    public bool IsClient { get; init; }
    public string RoleKey { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string? TrainerName { get; init; }
    public string? ProfessionalName { get; init; }
    public required Avatar Avatar { get; init; }
    public DateTime MemberSince { get; init; }
    public DateTime? LastLoginAt { get; init; }
}

public class ForgotPasswordViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordWithCodeViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8)]
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm new password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class NotificationSettingsViewModel
{
    [Display(Name = "Appointment updates and reminders")]
    public bool EmailAppointments { get; set; }

    [Display(Name = "Workout and meal reminders")]
    public bool EmailReminders { get; set; }

    [Display(Name = "New plans, feedback and trainer changes")]
    public bool EmailUpdates { get; set; }

    [Display(Name = "Remind me to log workouts")]
    public bool RemindWorkouts { get; set; }

    [Display(Name = "Remind me to log meals")]
    public bool RemindMeals { get; set; }
}
