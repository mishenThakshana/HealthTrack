using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace HealthTrack.Services;

// Sends queued emails in the background so requests never wait on SMTP.
public class EmailSenderService(EmailQueue queue, IOptions<EmailOptions> options, ILogger<EmailSenderService> logger) : BackgroundService
{
    private const int MaxAttempts = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in queue.ReadAllAsync(stoppingToken))
        {
            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    await SendAsync(message, stoppingToken);
                    break;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    if (attempt == MaxAttempts)
                        logger.LogError(ex, "Giving up on email \"{Subject}\" to {To}", message.Subject, message.ToAddress);
                    else
                        await Task.Delay(TimeSpan.FromSeconds(5 * attempt), stoppingToken);
                }
            }
        }
    }

    private async Task SendAsync(EmailMessage message, CancellationToken token)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            logger.LogInformation("Email disabled; skipped \"{Subject}\" to {To}", message.Subject, message.ToAddress);
            return;
        }

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        mime.To.Add(new MailboxAddress(message.ToName, message.ToAddress));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { HtmlBody = message.HtmlBody, TextBody = message.TextBody }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(settings.Host, settings.Port,
            settings.UseSsl ? SecureSocketOptions.Auto : SecureSocketOptions.None, token);
        if (!string.IsNullOrEmpty(settings.UserName))
            await client.AuthenticateAsync(settings.UserName, settings.Password ?? string.Empty, token);
        await client.SendAsync(mime, token);
        await client.DisconnectAsync(true, token);

        logger.LogInformation("Sent email \"{Subject}\" to {To}", message.Subject, message.ToAddress);
    }
}
