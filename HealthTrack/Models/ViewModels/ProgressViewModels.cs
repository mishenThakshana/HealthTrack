using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.ViewModels;

public record ActivitySummary(string Activity, int Sessions, int Minutes, int Clients = 1);

public record FeedbackItem(int Id, string AuthorId, string AuthorName, string AuthorRole, string Message, DateTime CreatedAt);

public class FeedbackFormViewModel
{
    [Required(ErrorMessage = "Please write some feedback.")]
    [StringLength(1000, MinimumLength = 5, ErrorMessage = "Use between 5 and 1000 characters.")]
    [Display(Name = "Feedback for the client")]
    public string Message { get; set; } = string.Empty;
}

public class ProgressViewModel
{
    public required ApplicationUser Client { get; init; }
    public bool IsOwn { get; init; }
    public bool CanGiveFeedback { get; init; }
    public string CurrentUserId { get; init; } = string.Empty;
    public int Days { get; init; }

    public decimal? CurrentWeight { get; init; }
    public decimal? WeightChange { get; init; }
    public decimal? Bmi { get; init; }
    public int Workouts { get; init; }
    public int WorkoutMinutes { get; init; }
    public int CaloriesBurned { get; init; }
    public int? AverageCaloriesEaten { get; init; }
    public int? CalorieTarget { get; init; }
    public int ActiveDays { get; init; }

    public required ChartData Weight { get; init; }
    public required ChartData BmiTrend { get; init; }
    public required ChartData BodyFat { get; init; }
    public required ChartData HeartRate { get; init; }
    public required ChartData Minutes { get; init; }
    public required ChartData Burned { get; init; }
    public required ChartData Eaten { get; init; }

    public IReadOnlyList<ActivitySummary> TopActivities { get; init; } = [];
    public IReadOnlyList<FeedbackItem> Feedback { get; init; } = [];
    public FeedbackFormViewModel FeedbackForm { get; init; } = new();
}
