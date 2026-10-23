using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.ViewModels;

public class ReviewFormViewModel
{
    [Required(ErrorMessage = "Please choose a rating from 1 to 5 stars.")]
    [Range(1, 5, ErrorMessage = "Please choose a rating from 1 to 5 stars.")]
    public int? Rating { get; set; }

    [StringLength(1000)]
    [Display(Name = "Your feedback (optional)")]
    public string? Comment { get; set; }
}

// Average star rating and number of reviews; Average is null when there are none.
public record RatingSummary(double? Average, int Count)
{
    public string Label => Average is { } avg ? $"{avg:0.0} ({Count} review{(Count == 1 ? "" : "s")})" : "No reviews yet";
}

// A review as shown on a trainer's profile. Clients are shown by first name and initial only.
public record ReviewItem(int Id, int AppointmentId, string ClientName, int Rating, string? Comment, DateTime CreatedAt)
{
    public string ClientShortName
    {
        get
        {
            var parts = ClientName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length switch
            {
                0 => "Client",
                1 => parts[0],
                _ => $"{parts[0]} {char.ToUpperInvariant(parts[^1][0])}."
            };
        }
    }
}

public class ProviderProfileViewModel
{
    public required ProviderCard Provider { get; init; }
    public RatingSummary Rating => new(Provider.AverageRating, Provider.ReviewCount);
    public IReadOnlyList<int> StarCounts { get; init; } = [];
    public IReadOnlyList<ReviewItem> Reviews { get; init; } = [];
    public bool IsClient { get; init; }
    public bool IsAdmin { get; init; }
    public bool IsSelf { get; init; }
    public bool IsMine { get; init; }
    public string? CurrentChoiceName { get; init; }
}

// A completed appointment the client hasn't rated yet.
public record ReviewPrompt(int AppointmentId, string ProviderName, DateTime StartsAt);
