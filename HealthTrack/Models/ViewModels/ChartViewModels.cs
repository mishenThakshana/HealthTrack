namespace HealthTrack.Models.ViewModels;

public record ChartSeries(string Label, IReadOnlyList<decimal?> Values, string Kind = "line");

public record ChartData(string Id, IReadOnlyList<string> Labels, IReadOnlyList<ChartSeries> Series, string? YTitle = null, bool Stacked = false)
{
    public bool HasData => Series.Any(s => s.Kind != "reference" && s.Values.Any(v => v is not null and not 0));
}

// The period controls on the Reports and Progress pages: presets, custom dates and export buttons.
// RouteId is the client's id when a trainer views a client's progress.
public record RangePicker(Helpers.DateRange Range, string? RouteId = null);
