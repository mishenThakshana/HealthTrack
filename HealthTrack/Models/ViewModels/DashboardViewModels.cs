namespace HealthTrack.Models.ViewModels;

public record StatTile(string Icon, string Label, string Value, string Hint);

public record EmptyState(string Icon, string Message);

public record RecentUser(string FullName, string Email, string Role, DateTime CreatedAt, DateTime? LastLoginAt);

public record UsageTotal(string Icon, string Label, int Total, int ThisWeek);

public class AdminDashboardViewModel
{
    public int TotalUsers { get; init; }
    public int Clients { get; init; }
    public int Trainers { get; init; }
    public int ActiveThisWeek { get; init; }
    public int NewThisWeek { get; init; }
    public int LockedOut { get; init; }
    public IReadOnlyList<RecentUser> RecentUsers { get; init; } = [];
    public int AssignedClients { get; init; }
    public IReadOnlyList<TrainerLoad> TrainerLoads { get; init; } = [];
    public required ChartData Usage { get; init; }
    public IReadOnlyList<UsageTotal> UsageTotals { get; init; } = [];
}

public class MemberDashboardViewModel
{
    public required Avatar Avatar { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public int? Age { get; init; }
    public string? Gender { get; init; }
    public DateTime MemberSince { get; init; }
    public IReadOnlyList<(string Label, string Value)> Details { get; init; } = [];
    public bool DetailsMissing { get; init; }
    // Trainers only: their reviews from clients.
    public RatingSummary? Rating { get; init; }

    public string FirstName =>
        FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).SkipWhile(p => p.EndsWith('.')).FirstOrDefault() ?? FullName;
}

public class ClientDashboardViewModel
{
    public required MemberDashboardViewModel Profile { get; init; }
    public ProviderCard? Trainer { get; init; }
    public IReadOnlyList<ReviewPrompt> ReviewPrompts { get; init; } = [];
    public HealthMeasurement? LatestMeasurement { get; init; }
    public decimal? Bmi { get; init; }
    public int WorkoutsThisWeek { get; init; }
    public int MinutesThisWeek { get; init; }
    public int CaloriesToday { get; init; }
    public PlanScheduleViewModel Plan { get; init; } = new();
    public IReadOnlyList<WorkoutLog> RecentWorkouts { get; init; } = [];
    public IReadOnlyList<Appointment> UpcomingAppointments { get; init; } = [];
    public IReadOnlyList<FeedbackItem> LatestFeedback { get; init; } = [];
}

public class TrainerDashboardViewModel
{
    public required MemberDashboardViewModel Profile { get; init; }
    public IReadOnlyList<ClientSummary> Clients { get; init; } = [];
    public int ActivePlans { get; init; }
    public int ClientWorkoutsThisWeek { get; init; }
    public IReadOnlyList<Appointment> TodaysAppointments { get; init; } = [];
    public IReadOnlyList<Appointment> AppointmentRequests { get; init; } = [];
}

public record DeleteButton(string Controller, string Action, Dictionary<string, string> RouteValues, string Confirm, string? Label = null);
