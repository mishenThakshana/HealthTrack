using System.Net;

namespace HealthTrack.Services;

public static class EmailTemplates
{
    public static (string Html, string Text) Render(string greetingName, string heading, IEnumerable<string> paragraphs,
        string? buttonText = null, string? buttonUrl = null, string? footer = null)
    {
        var lines = paragraphs.ToList();
        static string e(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

        var button = buttonUrl is null ? "" : $"""
            <p style="margin:24px 0">
              <a href="{e(buttonUrl)}" style="background:#ea580c;color:#ffffff;text-decoration:none;font-weight:600;padding:12px 20px;border-radius:8px;display:inline-block">{e(buttonText ?? "Open HealthTrack")}</a>
            </p>
            <p style="color:#6b7280;font-size:13px">If the button doesn't work, copy this link into your browser:<br><span style="word-break:break-all">{e(buttonUrl)}</span></p>
            """;

        var html = $"""
            <!doctype html>
            <html><body style="margin:0;background:#fff7ed;font-family:Inter,Segoe UI,Arial,sans-serif;color:#1f2937">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0"><tr><td align="center" style="padding:24px 12px">
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:560px;background:#ffffff;border:1px solid #f1e4d8;border-radius:12px">
                  <tr><td style="background:#ea580c;color:#ffffff;padding:16px 24px;border-radius:12px 12px 0 0;font-weight:800;font-size:18px">HealthTrack</td></tr>
                  <tr><td style="padding:24px">
                    <h1 style="font-size:20px;margin:0 0 16px">{e(heading)}</h1>
                    <p>Hi {e(greetingName)},</p>
                    {string.Concat(lines.Select(l => $"<p style=\"line-height:1.5\">{e(l)}</p>"))}
                    {button}
                    <p style="color:#6b7280;font-size:13px;margin-top:24px">{e(footer ?? "You're receiving this because you have a HealthTrack account.")}</p>
                  </td></tr>
                </table>
              </td></tr></table>
            </body></html>
            """;

        var text = string.Join("\n\n", new[] { $"Hi {greetingName},", heading }
            .Concat(lines)
            .Concat(buttonUrl is null ? [] : [$"{buttonText ?? "Open HealthTrack"}: {buttonUrl}"])
            .Append(footer ?? "You're receiving this because you have a HealthTrack account."));

        return (html, text);
    }
}
