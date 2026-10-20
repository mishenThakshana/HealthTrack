namespace HealthTrack.Models.ViewModels;

public class NotificationBellViewModel
{
    public int Unread { get; init; }
    public IReadOnlyList<Notification> Latest { get; init; } = [];
}
