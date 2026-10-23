using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HealthTrack.Models.ViewModels;

public abstract class AppointmentSlotViewModel
{
    [Required, DataType(DataType.Date)]
    public DateOnly Date { get; set; } = Helpers.Dates.Today.AddDays(1);

    [Required(ErrorMessage = "Please choose a time.")]
    [DataType(DataType.Time)]
    public TimeOnly? Time { get; set; }

    [Display(Name = "Duration")]
    public int DurationMinutes { get; set; } = 60;

    [Display(Name = "Type")]
    public AppointmentMode Mode { get; set; }

    public DateTime StartsAt => Date.ToDateTime(Time ?? default);
    public DateTime EndsAt => StartsAt.AddMinutes(DurationMinutes);
}

public class AppointmentRequestViewModel : AppointmentSlotViewModel
{
    [Required(ErrorMessage = "Please choose who you'd like to see.")]
    [Display(Name = "Trainer")]
    public string TrainerId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please say what you'd like to discuss.")]
    [StringLength(300, MinimumLength = 5, ErrorMessage = "Use between 5 and 300 characters.")]
    [Display(Name = "What would you like to discuss?")]
    public string Reason { get; set; } = string.Empty;
}

public class AppointmentEditViewModel : AppointmentSlotViewModel
{
    [StringLength(200)]
    [Display(Name = "Location or meeting link")]
    public string? Location { get; set; }

    [StringLength(300)]
    [Display(Name = "Note to the client")]
    public string? TrainerNote { get; set; }
}

public class AppointmentsPageViewModel
{
    public bool IsProvider { get; init; }
    public bool IsAdmin { get; init; }
    public AppointmentRequestViewModel Form { get; init; } = new();
    public IEnumerable<SelectListItem> Trainers { get; init; } = [];
    public IReadOnlyList<Appointment> Requests { get; init; } = [];
    public IReadOnlyList<Appointment> Upcoming { get; init; } = [];
    public IReadOnlyList<Appointment> History { get; init; } = [];
}

public class AppointmentDetailsViewModel
{
    public required Appointment Appointment { get; init; }
    public bool IsProvider { get; init; }
    public bool IsAdmin { get; init; }
    public AppointmentReview? Review { get; init; }
    public ReviewFormViewModel ReviewForm { get; init; } = new();
    // Shows the review form even when a review exists (after "Edit", or when the form had errors).
    public bool EditingReview { get; init; }
}

public class AppointmentEditPageViewModel
{
    public required Appointment Appointment { get; init; }
    public AppointmentEditViewModel Form { get; init; } = new();
}
