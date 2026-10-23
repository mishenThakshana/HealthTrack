namespace HealthTrack.Helpers;

public record Bucket(DateOnly Start, DateOnly End, string Label)
{
    public bool Contains(DateOnly date) => date >= Start && date <= End;
}

// The period a report or progress page covers: one of the preset "last N days" ranges, or dates the user picked.
public record DateRange(DateOnly From, DateOnly To, bool IsCustom = false)
{
    public int Days => To.DayNumber - From.DayNumber + 1;

    public bool Contains(DateOnly date) => date >= From && date <= To;

    public string Label => IsCustom ? $"{From:d MMM yyyy} – {To:d MMM yyyy}" : Periods.Label(Days);

    // Used in export file names, e.g. "2026-09-01-to-2026-09-30" or "30d".
    public string FileSuffix => IsCustom ? $"{From:yyyy-MM-dd}-to-{To:yyyy-MM-dd}" : $"{Days}d";

    // Route values that reproduce this range in a link: either days=N or from/to in ISO format.
    public int? RouteDays => IsCustom ? null : Days;
    public string? RouteFrom => IsCustom ? From.ToString("yyyy-MM-dd") : null;
    public string? RouteTo => IsCustom ? To.ToString("yyyy-MM-dd") : null;
}

public static class Periods
{
    public static readonly int[] Ranges = [30, 90, 180, 365];
    public const int DefaultRange = 90;

    // Longest custom range, so charts and exports stay a sensible size.
    public const int MaxCustomDays = 731;

    public static int Normalize(int days) => Ranges.Contains(days) ? days : DefaultRange;

    public static DateOnly From(int days) => Dates.Today.AddDays(1 - days);

    public static string Label(int days) => days switch
    {
        365 => "Last 12 months",
        180 => "Last 6 months",
        _ => $"Last {days} days"
    };

    public static DateRange Last(int days) => new(From(days), Dates.Today);

    // Uses the custom dates when both are given and valid; otherwise the preset (or the default preset).
    // Error explains why custom dates were ignored, for showing on the page.
    public static DateRange Resolve(int days, DateOnly? from, DateOnly? to, int defaultDays, out string? error)
    {
        error = null;
        var preset = Last(Ranges.Contains(days) ? days : defaultDays);
        if (from is null && to is null)
            return preset;

        if (from is not { } start || to is not { } end)
            error = "Choose both a start and an end date.";
        else if (start > end)
            error = "The start date must be on or before the end date.";
        else if (end > Dates.Today)
            error = "The end date can't be in the future.";
        else if (end.DayNumber - start.DayNumber + 1 > MaxCustomDays)
            error = "Choose a range of up to 2 years.";
        else
            return new DateRange(start, end, IsCustom: true);

        return preset;
    }

    public static List<Bucket> Days(DateRange range) =>
        Enumerable.Range(0, range.Days)
            .Select(i => range.From.AddDays(i))
            .Select(d => new Bucket(d, d, d.ToString(range.Days > 180 ? "d MMM yy" : "d MMM")))
            .ToList();

    public static List<Bucket> Weeks(DateRange range)
    {
        var start = range.From.AddDays(-(((int)range.From.DayOfWeek + 6) % 7));
        var buckets = new List<Bucket>();
        for (var week = start; week <= range.To; week = week.AddDays(7))
        {
            var end = week.AddDays(6);
            buckets.Add(new Bucket(week < range.From ? range.From : week, end > range.To ? range.To : end,
                $"Wk {week.ToString(range.Days > 365 ? "d MMM yy" : "d MMM")}"));
        }
        return buckets;
    }

    public static List<Bucket> Buckets(DateRange range, bool weekly) => weekly ? Weeks(range) : Days(range);
}
