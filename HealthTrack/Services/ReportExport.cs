using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HealthTrack.Services;

// One table in an exported report: a sheet in Excel, a section in the PDF.
public record ExportTable(string Name, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<object?>> Rows);

public record ExportReport(string Title, string Subtitle, IReadOnlyList<(string Label, object? Value)> Summary, IReadOnlyList<ExportTable> Tables);

// Turns a report into an Excel workbook or a PDF. Values keep their type (numbers, dates) so Excel can sort and sum them.
public static class ReportExport
{
    public const string Excel = "xlsx";
    public const string Pdf = "pdf";

    private const string Orange = "#EA580C";

    public static bool IsFormat(string? format) => format is Excel or Pdf;

    public static FileContentResult ToFile(ExportReport report, string format, string fileName) => format == Pdf
        ? new FileContentResult(ToPdf(report), "application/pdf") { FileDownloadName = $"{fileName}.pdf" }
        : new FileContentResult(ToExcel(report), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet") { FileDownloadName = $"{fileName}.xlsx" };

    public static byte[] ToExcel(ExportReport report)
    {
        using var workbook = new XLWorkbook();

        var summary = workbook.Worksheets.Add("Summary");
        summary.Cell(1, 1).Value = report.Title;
        summary.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(16).Font.SetFontColor(XLColor.FromHtml(Orange));
        summary.Cell(2, 1).Value = report.Subtitle;
        summary.Cell(2, 1).Style.Font.SetItalic();
        var row = 4;
        foreach (var (label, value) in report.Summary)
        {
            summary.Cell(row, 1).Value = label;
            summary.Cell(row, 1).Style.Font.SetBold();
            Set(summary.Cell(row, 2), value);
            summary.Cell(row, 2).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left);
            row++;
        }
        summary.Column(1).AdjustToContents();
        summary.Column(2).Width = 40;

        foreach (var table in report.Tables)
        {
            var sheet = workbook.Worksheets.Add(SheetName(table.Name, workbook));
            for (var c = 0; c < table.Headers.Count; c++)
                sheet.Cell(1, c + 1).Value = table.Headers[c];

            var header = sheet.Range(1, 1, 1, table.Headers.Count).Style;
            header.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(XLColor.FromHtml(Orange));

            for (var r = 0; r < table.Rows.Count; r++)
                for (var c = 0; c < table.Rows[r].Count; c++)
                    Set(sheet.Cell(r + 2, c + 1), table.Rows[r][c]);

            if (table.Rows.Count > 0)
                sheet.Range(1, 1, table.Rows.Count + 1, table.Headers.Count).SetAutoFilter();
            sheet.SheetView.FreezeRows(1);
            sheet.Columns(1, table.Headers.Count).AdjustToContents(10.0, 60.0);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static byte[] ToPdf(ExportReport report) =>
        Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(28);
            page.DefaultTextStyle(text => text.FontSize(9));

            page.Header().PaddingBottom(10).Column(header =>
            {
                header.Item().Text(report.Title).FontSize(18).Bold().FontColor(Orange);
                header.Item().Text(report.Subtitle).FontColor(Colors.Grey.Darken1);
            });

            page.Content().Column(content =>
            {
                content.Spacing(16);

                content.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(170);
                        columns.RelativeColumn();
                    });
                    foreach (var (label, value) in report.Summary)
                    {
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(label).SemiBold();
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(Format(value));
                    }
                });

                foreach (var section in report.Tables)
                {
                    content.Item().Column(block =>
                    {
                        block.Item().PaddingBottom(4).Text(section.Name).FontSize(12).SemiBold().FontColor(Orange);
                        if (section.Rows.Count == 0)
                        {
                            block.Item().Text("No entries in this period.").Italic().FontColor(Colors.Grey.Darken1);
                            return;
                        }

                        block.Item().Table(table =>
                        {
                            // Wider columns for longer text (emails, descriptions), within limits so short columns stay readable.
                            table.ColumnsDefinition(columns =>
                            {
                                for (var c = 0; c < section.Headers.Count; c++)
                                {
                                    var longest = section.Rows.Select(r => Format(r[c]).Length)
                                        .Append(section.Headers[c].Split(' ').Max(w => w.Length)).Max();
                                    columns.RelativeColumn(Math.Clamp(longest, 9, 30));
                                }
                            });
                            table.Header(header =>
                            {
                                foreach (var title in section.Headers)
                                    header.Cell().Background(Orange).Padding(4).Text(title).FontColor(Colors.White).SemiBold();
                            });
                            foreach (var row in section.Rows)
                                foreach (var value in row)
                                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(Format(value));
                        });
                    });
                }
            });

            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("HealthTrack · page ").FontColor(Colors.Grey.Darken1);
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
            });
        })).GeneratePdf();

    private static void Set(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                break;
            case DateOnly date:
                cell.Value = date.ToDateTime(TimeOnly.MinValue);
                cell.Style.DateFormat.Format = "d mmm yyyy";
                break;
            case DateTime time:
                cell.Value = time;
                cell.Style.DateFormat.Format = "d mmm yyyy hh:mm";
                break;
            case int number:
                cell.Value = number;
                break;
            case decimal number:
                cell.Value = (double)number;
                break;
            case double number:
                cell.Value = number;
                break;
            default:
                // Set as text, so a value starting with "=" is never treated as a formula.
                cell.Value = value.ToString();
                break;
        }
    }

    private static string Format(object? value) => value switch
    {
        null => "–",
        DateOnly date => date.ToString("d MMM yyyy"),
        DateTime time => time.ToString("d MMM yyyy HH:mm"),
        decimal number => number.ToString("0.#"),
        double number => number.ToString("0.#"),
        _ => value.ToString() ?? ""
    };

    // Excel sheet names: at most 31 characters, no []:*?/\ and unique in the workbook.
    private static string SheetName(string name, XLWorkbook workbook)
    {
        var clean = new string(name.Where(c => !"[]:*?/\\".Contains(c)).ToArray());
        clean = clean.Length > 31 ? clean[..31] : clean;
        var unique = clean;
        for (var i = 2; workbook.Worksheets.Contains(unique); i++)
            unique = $"{clean[..Math.Min(clean.Length, 28)]} {i}";
        return unique;
    }
}
