using HealthTrack.Helpers;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace HealthTrack.Controllers;

internal static class TrackingRules
{
    public const int PageSize = 50;

    public static void ValidateDate(ModelStateDictionary modelState, DateOnly date, DateOnly? original = null)
    {
        if (date > Dates.Today)
            modelState.AddModelError("Form.Date", "Date can't be in the future.");
        else if (date < Dates.Today.AddYears(-1) && date != original)
            modelState.AddModelError("Form.Date", "Date can't be more than a year ago.");
    }

    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
