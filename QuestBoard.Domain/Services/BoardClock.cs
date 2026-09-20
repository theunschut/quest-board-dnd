using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Services;

// Resolves the configured board time zone exactly once, at construction, so every consumer of
// IBoardClock reads the same TimeZoneInfo for the lifetime of the process rather than each
// resolving it independently. An unresolvable id degrades to UTC instead of crashing the
// application, because a wrong-looking timestamp is a far smaller problem than a board that
// cannot boot.
internal sealed class BoardClock(TimeProvider timeProvider, IOptions<TimeZoneOptions> options, ILogger<BoardClock> logger) : IBoardClock
{
    private static (TimeZoneInfo Zone, bool Degraded) ResolveZone(string zoneId, ILogger logger)
    {
        try
        {
            return (TimeZoneInfo.FindSystemTimeZoneById(zoneId), false);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            logger.LogWarning(
                ex,
                "Configured board time zone '{ZoneId}' could not be resolved; the board clock has fallen back to UTC.",
                zoneId);
            return (TimeZoneInfo.Utc, true);
        }
    }

    private readonly (TimeZoneInfo Zone, bool Degraded) _resolved = ResolveZone(options.Value.BoardTimeZoneId, logger);

    public TimeZoneInfo TimeZone => _resolved.Zone;

    public bool IsDegraded => _resolved.Degraded;

    public DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(timeProvider.GetUtcNow().UtcDateTime, TimeZone);

    public DateOnly Today => DateOnly.FromDateTime(Now);
}
