using BestStories.Api.Clients.HackerNews;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BestStories.Api.Health;

/// <summary>
/// Readiness check: verifies the upstream Hacker News API is reachable.
/// Goes through the decorated client, so a warm cache answers cheaply.
/// </summary>
public sealed class HackerNewsHealthCheck(IHackerNewsClient client) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await client.GetBestStoryIdsAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("Hacker News API unreachable", ex);
        }
    }
}
