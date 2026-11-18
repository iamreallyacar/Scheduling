using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace TireScheduler.Api.Tests;

internal sealed record RecordedLogEntry(LogLevel Level, string Message, Exception? Exception);

// Keeps log entries in memory so a test can check what was logged.
internal sealed class RecordingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<RecordedLogEntry> entries = new();

    public IReadOnlyCollection<RecordedLogEntry> Entries => entries;

    public ILogger CreateLogger(string categoryName) => new RecordingLogger(entries);

    public void Dispose()
    {
    }

    private sealed class RecordingLogger(ConcurrentQueue<RecordedLogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            entries.Enqueue(new RecordedLogEntry(logLevel, formatter(state, exception), exception));
    }
}
