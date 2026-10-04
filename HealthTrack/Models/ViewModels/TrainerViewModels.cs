namespace HealthTrack.Models.ViewModels;

public record ClientSummary(
    string Id, string FullName, string Email, int? Age, decimal? LatestWeightKg,
    int WorkoutsThisWeek, int ActivePlans, DateOnly? LastActivity);

public class ClientDetailsViewModel
{
    public required ApplicationUser Client { get; init; }
    public int? Age { get; init; }
    public IReadOnlyList<HealthMeasurement> Measurements { get; init; } = [];
    public IReadOnlyList<WorkoutLog> WorkoutLogs { get; init; } = [];
    public IReadOnlyList<MealLog> MealLogs { get; init; } = [];
    public IReadOnlyList<PlanListItem> WorkoutPlans { get; init; } = [];
    public IReadOnlyList<PlanListItem> DietPlans { get; init; } = [];
}

public record AssignmentRow(string ClientId, string ClientName, string ClientEmail, string? TrainerId, string? TrainerName);

public record TrainerLoad(string Id, string FullName, int ClientCount);

public class TrainerAssignmentsViewModel
{
    public IReadOnlyList<AssignmentRow> Clients { get; init; } = [];
    public IReadOnlyList<TrainerLoad> Trainers { get; init; } = [];
}
