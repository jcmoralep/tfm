using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace BmadPlatform.Application.Tests.TestDoubles;

public sealed record CapturedLogEntry(
    string Category,
    LogLevel Level,
    string Message,
    IReadOnlyList<KeyValuePair<string, object?>> Properties,
    Exception? Exception)
{
    /// <summary>Every piece of text this entry would expose to a log sink.</summary>
    public string AllText =>
        string.Join(
            " | ",
            new[] { Message, Exception?.ToString() ?? string.Empty }
                .Concat(Properties.Select(property => $"{property.Key}={property.Value}")));
}

/// <summary>Logger provider that records every entry, including structured properties.</summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<CapturedLogEntry> entries = new();

    public IReadOnlyList<CapturedLogEntry> Entries => entries.ToArray();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, entries);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(string category, ConcurrentQueue<CapturedLogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = state as IReadOnlyList<KeyValuePair<string, object?>> ?? [];
            entries.Enqueue(new CapturedLogEntry(category, logLevel, formatter(state, exception), properties, exception));
        }
    }
}
