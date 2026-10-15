using System.Threading.Channels;
using RutaProducto.Application.Abstractions.Email;
using Microsoft.Extensions.Logging;

namespace RutaProducto.Infrastructure.Email;

/// <summary>In-process bounded queue between the request side and the dispatcher. When it is full the newest message is dropped.</summary>
internal sealed class EmailQueue
{
    public const int DefaultCapacity = 100;

    private readonly int capacity;
    private readonly Channel<EmailMessage> channel;

    public EmailQueue(int capacity = DefaultCapacity)
    {
        this.capacity = capacity;
        channel = Channel.CreateBounded<EmailMessage>(
            new BoundedChannelOptions(capacity) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });
    }

    public ChannelReader<EmailMessage> Reader => channel.Reader;

    /// <summary>False when the queue is full. DropWrite alone would report success for a message it discarded.</summary>
    public bool TryWrite(EmailMessage message) => channel.Reader.Count < capacity && channel.Writer.TryWrite(message);
}

/// <summary>Hands each message to the queue and returns at once; delivery happens later in <see cref="EmailDispatcher"/>.</summary>
internal sealed class QueuedEmailSender(EmailQueue queue, ILogger<QueuedEmailSender> logger) : IEmailSender
{
    public void Enqueue(EmailMessage message)
    {
        if (!queue.TryWrite(message))
        {
            logger.LogWarning("Email queue is full; {Kind} dropped", message.Kind);
        }
    }
}

/// <summary>Email is off (<c>Email:Provider=None</c>): the message is accepted and discarded, nothing leaves the process.</summary>
internal sealed class DisabledEmailSender(ILogger<DisabledEmailSender> logger) : IEmailSender
{
    public void Enqueue(EmailMessage message) =>
        logger.LogInformation("Email {Kind} not sent: Email:Provider is None", message.Kind);
}
