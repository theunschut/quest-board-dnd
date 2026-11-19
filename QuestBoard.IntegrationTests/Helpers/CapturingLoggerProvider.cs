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
//
// It also takes part in the logging scope sharing the framework offers providers, so the scopes
// open around a record (the hosting layer's request id and path, for one) are captured beside it
// in Entries. Records stays exactly as it was: one string per record, scopes not in it.
public class CapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentQueue<string> _records = new();
    private readonly ConcurrentQueue<CapturedLogEntry> _entries = new();
    private volatile IExternalScopeProvider? _scopeProvider;

    public IReadOnlyList<string> Records => _records.ToArray();

    /// <summary>Every record with the key-value pairs of the scopes that were open when it was written.</summary>
    public IReadOnlyList<CapturedLogEntry> Entries => _entries.ToArray();

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopeProvider = scopeProvider;

    public void Clear()
    {
        // ConcurrentQueue has no atomic Clear in older runtimes, but this codebase targets a
        // modern one; draining via TryDequeue avoids replacing the field under concurrent
        // writers that already hold a reference to it.
        while (_records.TryDequeue(out _))
        {
        }

        while (_entries.TryDequeue(out _))
        {
        }
    }

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _records, _entries, () => _scopeProvider);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(
        string categoryName,
        ConcurrentQueue<string> records,
        ConcurrentQueue<CapturedLogEntry> entries,
        Func<IExternalScopeProvider?> scopeProvider) : ILogger
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

            var scopePairs = new List<KeyValuePair<string, object?>>();
            scopeProvider()?.ForEachScope(
                static (scope, pairs) =>
                {
                    if (scope is IEnumerable<KeyValuePair<string, object?>> scopeState)
                    {
                        pairs.AddRange(scopeState);
                    }
                },
                scopePairs);

            entries.Enqueue(new CapturedLogEntry(categoryName, logLevel, formatter(state, exception), scopePairs));
        }
    }
}

/// <summary>One captured log record and the key-value pairs of every scope open when it was written.</summary>
public sealed record CapturedLogEntry(
    string Category,
    LogLevel Level,
    string Message,
    IReadOnlyList<KeyValuePair<string, object?>> ScopePairs);
