using System.Diagnostics;
using BestStories.Api.Common.Observability;

namespace BestStories.Api.Clients.HackerNews;

/// <summary>
/// Decorator: metrics + structured logging for upstream calls. Registered inside the
/// cache decorator, so it measures only real outbound traffic (cache hits bypass it).
/// </summary>
public sealed class InstrumentedHackerNewsClient(
    IHackerNewsClient inner,
    HackerNewsMetrics metrics,
    ILogger<InstrumentedHackerNewsClient> logger) : IHackerNewsClient
{
    public async ValueTask<int[]> GetBestStoryIdsAsync(CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var result = await inner.GetBestStoryIdsAsync(cancellationToken);
            Record("beststories", "success", started);
            return result;
        }
        catch (Exception ex)
        {
            Record("beststories", "error", started);
            logger.LogWarning(ex, "Hacker News beststories call failed");
            throw;
        }
    }

    public async ValueTask<HackerNewsItem?> GetStoryAsync(int id, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var result = await inner.GetStoryAsync(id, cancellationToken);
            Record("item", result is null ? "not-found" : "success", started);
            return result;
        }
        catch (Exception ex)
        {
            Record("item", "error", started);
            logger.LogWarning(ex, "Hacker News item call failed for {StoryId}", id);
            throw;
        }
    }

    private void Record(string operation, string outcome, long startedTimestamp)
    {
        var elapsedMs = Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds;
        metrics.RecordCall(operation, outcome, elapsedMs);
        logger.LogDebug(
            "Hacker News {Operation} completed in {ElapsedMs:F1}ms ({Outcome})",
            operation, elapsedMs, outcome);
    }
}
