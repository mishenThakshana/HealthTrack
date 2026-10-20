namespace HealthTrack.Models.ViewModels;

public record ClientReportRow(
    string Id, string FullName, string Email, string? TrainerName,
    int Workouts, int Minutes, int CaloriesBurned, int? AverageCaloriesEaten, int Measurements,
    decimal? WeightChange, int ActiveDays, DateOnly? LastActivity, string Engagement);

public record ExerciseUsage(string Name, int Plans, int Clients);

public record StatusCount(string Status, int Count);

public class ReportsViewModel
{
    public int Days { get; init; }
    public bool IsAdmin { get; init; }
    public int TotalClients { get; init; }
    public int ActiveClients { get; init; }
    public decimal AverageActiveDaysPerWeek { get; init; }
    public int TotalEntries { get; init; }
    public int AppointmentsCompleted { get; init; }
    public IReadOnlyList<ClientReportRow> Clients { get; init; } = [];
    public required ChartData Engagement { get; init; }
    public IReadOnlyList<ExerciseUsage> TopExercises { get; init; } = [];
    public IReadOnlyList<ActivitySummary> TopActivities { get; init; } = [];
    public IReadOnlyList<StatusCount> Appointments { get; init; } = [];
}
