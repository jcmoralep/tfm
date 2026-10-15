using System.Diagnostics;
using System.Net.Mail;
using RutaProducto.Application.Abstractions.Email;
using RutaProducto.Application.Tests.TestDoubles;
using RutaProducto.Infrastructure.Email;
using Microsoft.Extensions.Logging;

namespace RutaProducto.Infrastructure.Tests.Email;

public sealed class EmailDispatcherTests
{
    private const string Address = "ana@example.com";

    [Fact]
    public async Task Enqueue_returns_at_once_and_the_transport_gets_each_message_exactly_once()
    {
        var transport = new FakeTransport(delay: TimeSpan.FromSeconds(1));
        var (sender, dispatcher, _) = Build(transport);
        await dispatcher.StartAsync(CancellationToken.None);

        var clock = Stopwatch.StartNew();
        sender.Enqueue(Message(EmailKind.ConfirmEmail));
        sender.Enqueue(Message(EmailKind.ResetPassword));
        clock.Stop();

        Assert.True(clock.ElapsedMilliseconds < 500, "Enqueue must not wait for the transport.");

        await transport.WaitForAsync(2);
        await dispatcher.StopAsync(CancellationToken.None);

        Assert.Equal([EmailKind.ConfirmEmail, EmailKind.ResetPassword], transport.Sent.Select(message => message.Kind));
    }

    [Fact]
    public async Task A_failing_send_is_logged_without_the_address_and_the_next_message_still_goes()
    {
        var transport = new FakeTransport { FailFirstWith = new SmtpException(SmtpStatusCode.MailboxUnavailable, $"550 {Address} rejected") };
        var (sender, dispatcher, logs) = Build(transport);
        await dispatcher.StartAsync(CancellationToken.None);

        sender.Enqueue(Message(EmailKind.AccountExists));
        sender.Enqueue(Message(EmailKind.ConfirmEmail));
        await transport.WaitForAsync(2);
        await dispatcher.StopAsync(CancellationToken.None);

        Assert.Equal(EmailKind.ConfirmEmail, transport.Sent.Last().Kind);

        var text = string.Join(" ", logs.Entries.Select(entry => entry.AllText));
        Assert.Contains("AccountExists", text);
        Assert.Contains(nameof(SmtpException), text);
        Assert.Contains(nameof(SmtpStatusCode.MailboxUnavailable), text);
        Assert.DoesNotContain(Address, text);
        Assert.DoesNotContain("Subject", text);
        Assert.DoesNotContain("https://", text);
    }

    [Fact]
    public void The_101st_message_is_dropped_with_a_warning_that_carries_the_kind_only()
    {
        var logs = new CapturingLoggerProvider();
        var queue = new EmailQueue();
        var sender = new QueuedEmailSender(queue, LoggerFactory.Create(builder => builder.AddProvider(logs)).CreateLogger<QueuedEmailSender>());

        for (var count = 0; count < EmailQueue.DefaultCapacity + 1; count++)
        {
            sender.Enqueue(Message(EmailKind.ResetPassword));
        }

        var warning = Assert.Single(logs.Entries);
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Contains("ResetPassword", warning.AllText);
        Assert.DoesNotContain(Address, warning.AllText);
        Assert.Equal(EmailQueue.DefaultCapacity, queue.Reader.Count);
    }

    private static EmailMessage Message(EmailKind kind) =>
        new(Address, kind, "Subject line", "Body with https://app.example.com/link?codigo=abc");

    private static (QueuedEmailSender Sender, EmailDispatcher Dispatcher, CapturingLoggerProvider Logs) Build(FakeTransport transport)
    {
        var logs = new CapturingLoggerProvider();
        var factory = LoggerFactory.Create(builder => builder.AddProvider(logs));
        var queue = new EmailQueue();

        return (
            new QueuedEmailSender(queue, factory.CreateLogger<QueuedEmailSender>()),
            new EmailDispatcher(queue, transport, factory.CreateLogger<EmailDispatcher>()),
            logs);
    }

    private sealed class FakeTransport(TimeSpan delay = default) : IEmailTransport
    {
        private readonly List<EmailMessage> sent = [];
        private int attempts;

        public Exception? FailFirstWith { get; init; }

        public IReadOnlyList<EmailMessage> Sent
        {
            get
            {
                lock (sent)
                {
                    return sent.ToArray();
                }
            }
        }

        public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }

            var attempt = Interlocked.Increment(ref attempts);

            lock (sent)
            {
                sent.Add(message);
            }

            if (attempt == 1 && FailFirstWith is not null)
            {
                throw FailFirstWith;
            }
        }

        public async Task WaitForAsync(int count)
        {
            var limit = Stopwatch.StartNew();

            while (Sent.Count < count)
            {
                Assert.True(limit.Elapsed < TimeSpan.FromSeconds(10), "The dispatcher did not deliver in time.");
                await Task.Delay(20);
            }
        }
    }
}
