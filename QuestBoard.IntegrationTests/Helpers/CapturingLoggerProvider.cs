using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text;

namespace QuestBoard.IntegrationTests.Helpers;

// A log message can carry a sensitive value two ways: in the rendered message text, or as a
// structured state value the message template never names. A grep against a message template
// would only ever catch the first. This provider renders and captures both -- the category,
// the level, the fully rendered message, every structured state key and value, and the
// exception's own ToString() when present -- into one string per record, so a test asserting
// "no captured record contains the address" is actually checking everywhere the address could
// have gone, not just the part a human would read in a console.
public class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _records = new();

    public IReadOnlyList<string> Records => _records.ToArray();

    public void Clear()
    {
        // ConcurrentQueue has no atomic Clear in older runtimes, but this codebase targets a
        // modern one; draining via TryDequeue avoids replacing the field under concurrent
        // writers that already hold a reference to it.
        while (_records.TryDequeue(out _))
        {
        }
    }

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _records);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(string categoryName, ConcurrentQueue<string> records) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var builder = new StringBuilder();
            builder.Append('[').Append(logLevel).Append("] ").Append(categoryName).Append(": ");
            builder.Append(formatter(state, exception));

            // The rendered message alone would miss a value passed only as structured state --
            // the point of this class. IEnumerable<KeyValuePair<string, object>> is how the
            // logging abstraction exposes every named argument a structured log call captured,
            // including ones the message template string never mentions.
            if (state is IEnumerable<KeyValuePair<string, object?>> structuredState)
            {
                foreach (var pair in structuredState)
                {
                    builder.Append(" | ").Append(pair.Key).Append('=').Append(pair.Value);
                }
            }

            if (exception != null)
            {
                builder.Append(" | Exception=").Append(exception);
            }

            records.Enqueue(builder.ToString());
        }
    }
}
