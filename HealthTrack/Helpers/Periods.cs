namespace HealthTrack.Helpers;

public record Bucket(DateOnly Start, DateOnly End, string Label)
{
    public bool Contains(DateOnly date) => date >= Start && date <= End;
}

public static class Periods
{
    public static readonly int[] Ranges = [30, 90, 180, 365];
    public const int DefaultRange = 90;

    public static int Normalize(int days) => Ranges.Contains(days) ? days : DefaultRange;

    public static DateOnly From(int days) => Dates.Today.AddDays(1 - days);

    public static string Label(int days) => days switch
    {
        365 => "Last 12 months",
        180 => "Last 6 months",
        _ => $"Last {days} days"
    };

    public static List<Bucket> Days(int days)
    {
        var from = From(days);
        return Enumerable.Range(0, days)
            .Select(i => from.AddDays(i))
            .Select(d => new Bucket(d, d, d.ToString("d MMM")))
            .ToList();
    }

    public static List<Bucket> Weeks(int days)
    {
        var from = From(days);
        var start = from.AddDays(-(((int)from.DayOfWeek + 6) % 7));
        var buckets = new List<Bucket>();
        for (var week = start; week <= Dates.Today; week = week.AddDays(7))
            buckets.Add(new Bucket(week < from ? from : week, week.AddDays(6), $"Wk {week:d MMM}"));
        return buckets;
    }

    public static List<Bucket> Buckets(int days, bool weekly) => weekly ? Weeks(days) : Days(days);
}
