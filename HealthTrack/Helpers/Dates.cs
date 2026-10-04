namespace HealthTrack.Helpers;

public static class Dates
{
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    public static DateOnly StartOfWeek
    {
        get
        {
            var today = Today;
            var offset = ((int)today.DayOfWeek + 6) % 7;
            return today.AddDays(-offset);
        }
    }

    public static int? Age(DateOnly? dateOfBirth)
    {
        if (dateOfBirth is not { } dob)
            return null;

        var today = Today;
        var age = today.Year - dob.Year;
        return dob > today.AddYears(-age) ? age - 1 : age;
    }

    public static readonly DayOfWeek[] WeekOrder =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
    ];
}
