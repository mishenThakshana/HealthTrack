using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.ViewModels;

public record UserRow(
    string Id, string FullName, string Email, string Role, string? TrainerName,
    DateTime CreatedAt, DateTime? LastLoginAt, DateTimeOffset? LockoutEnd);

public class UsersIndexViewModel
{
    public string? Query { get; init; }
    public string? Role { get; init; }
    public IReadOnlyList<UserRow> Users { get; init; } = [];
    public int Total { get; init; }
}

public class UserFormViewModel
{
    [Required, StringLength(100, MinimumLength = 2)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = Data.Roles.Client;

    [DataType(DataType.Date)]
    [Display(Name = "Date of birth")]
    public DateOnly? DateOfBirth { get; set; }

    [StringLength(20)]
    public string? Gender { get; set; }

    [StringLength(100, MinimumLength = 8)]
    [DataType(DataType.Password)]
    public string? Password { get; set; }

    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm password")]
    public string? ConfirmPassword { get; set; }
}

public class ResetPasswordViewModel
{
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

public class UserEditPageViewModel
{
    public required string Id { get; init; }
    public UserFormViewModel Form { get; init; } = new();
    public ResetPasswordViewModel Reset { get; init; } = new();
    public bool IsSelf { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? LastLoginAt { get; init; }
    public DateTimeOffset? LockoutEnd { get; init; }
    public string? TrainerName { get; init; }
}
