namespace HealthTrack.Helpers;

public static class TimeAgo
{
    public static string Format(DateTime utc)
    {
        var span = DateTime.UtcNow - utc;
        return span.TotalMinutes switch
        {
            < 1 => "just now",
            < 60 => $"{(int)span.TotalMinutes} min ago",
            < 1440 => $"{(int)span.TotalHours} h ago",
            < 10080 => $"{(int)span.TotalDays} d ago",
            _ => utc.ToLocalTime().ToString("d MMM yyyy")
        };
    }
}
