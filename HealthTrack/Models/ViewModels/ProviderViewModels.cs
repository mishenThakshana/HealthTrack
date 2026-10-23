namespace HealthTrack.Models.ViewModels;

// A trainer as clients see them when browsing.
public record ProviderCard(
    string Id, string FullName, DateTime? PhotoUpdatedAt,
    string? Headline, int? YearsOfExperience, IReadOnlyList<string> Qualifications, int ClientCount,
    double? AverageRating, int ReviewCount)
{
    public Avatar Avatar(string size = "") => new(Id, FullName, PhotoUpdatedAt, size);
    public RatingSummary Rating => new(AverageRating, ReviewCount);
}

public class ProvidersPageViewModel
{
    public string? Search { get; init; }
    public string? Focus { get; init; }
    public bool SortByRating { get; init; }
    public IReadOnlyList<string> FocusOptions { get; init; } = [];
    public IReadOnlyList<ProviderCard> Providers { get; init; } = [];
    public ProviderCard? MyTrainer { get; init; }
}
