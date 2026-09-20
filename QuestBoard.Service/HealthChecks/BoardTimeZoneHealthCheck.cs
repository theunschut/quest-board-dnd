using Microsoft.Extensions.Diagnostics.HealthChecks;
using QuestBoard.Domain.Interfaces;

namespace QuestBoard.Service.HealthChecks;

// Reports whether the board clock resolved its configured time zone. Degraded, never Unhealthy:
// docker-compose's container healthcheck fails the container on any non-2xx response, so an
// Unhealthy result here would restart-loop a container whose only fault is a time zone typo.
internal sealed class BoardTimeZoneHealthCheck(IBoardClock boardClock) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        // The description strings below are fixed copy: the configured zone id, a host path, or
        // an exception message must never be interpolated in, since /health is reachable without
        // authentication.
        return Task.FromResult(boardClock.IsDegraded
            ? HealthCheckResult.Degraded("The board time zone could not be resolved; the board clock has fallen back to UTC.")
            : HealthCheckResult.Healthy("Board time zone resolved."));
    }
}
