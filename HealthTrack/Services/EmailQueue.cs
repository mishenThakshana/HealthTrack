using System.Threading.Channels;

namespace HealthTrack.Services;

public record EmailMessage(string ToAddress, string ToName, string Subject, string HtmlBody, string TextBody);

public class EmailQueue
{
    private readonly Channel<EmailMessage> channel = Channel.CreateUnbounded<EmailMessage>();

    public void Enqueue(EmailMessage message) => channel.Writer.TryWrite(message);

    public IAsyncEnumerable<EmailMessage> ReadAllAsync(CancellationToken token) => channel.Reader.ReadAllAsync(token);
}
