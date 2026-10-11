using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace QueryMentor.Tests;

internal sealed record LogEntry(string Category, LogLevel Level, string Message, Exception? Exception);

internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyList<LogEntry> Entries => [.. _entries];

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _entries);

    public async Task<LogEntry> WaitForAsync(Func<LogEntry, bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var match = _entries.FirstOrDefault(predicate);
            if (match is not null)
            {
                return match;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException("Expected log entry was not written. Entries:\n" + string.Join("\n", _entries.Select(e => $"{e.Level} {e.Category}: {e.Message}")));
    }

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => entries.Enqueue(new LogEntry(category, logLevel, formatter(state, exception), exception));
    }
}
