using System.Collections.Concurrent;
using System.Collections.Immutable;
using BestStories.Api.Clients.HackerNews;
using BestStories.Api.Configuration;
using FluentResults;
using Microsoft.Extensions.Options;

namespace BestStories.Api.Features.BestStories;

/// <summary>
/// Pure orchestration: fetch the ranked id list, take the first n, fan out for item
/// details with bounded parallelism, filter, sort by score, map to the response shape.
/// Caching and instrumentation live in IHackerNewsClient decorators, not here.
/// </summary>
public sealed class BestStoriesService(
    IHackerNewsClient client,
    IOptions<HackerNewsOptions> options,
    ILogger<BestStoriesService> logger)
{
    public async Task<Result<ImmutableArray<BestStoryResponse>>> GetBestStoriesAsync(
        int count,
        CancellationToken cancellationToken)
    {
        int[] ids;
        try
        {
            ids = await client.GetBestStoryIdsAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Failed to retrieve best story ids from Hacker News");
            return Result.Fail<ImmutableArray<BestStoryResponse>>(
                new UpstreamUnavailableError("Unable to reach the Hacker News API.", ex));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to retrieve best story ids from Hacker News");
            return Result.Fail<ImmutableArray<BestStoryResponse>>(
                new UpstreamUnavailableError("Unable to retrieve stories from the Hacker News API.", ex));
        }

        // The id list is already ranked best-first by HN — only fetch the n we need.
        var selectedIds = ids.AsSpan(0, Math.Min(count, ids.Length)).ToArray();

        var stories = new ConcurrentBag<BestStoryResponse>();
        var failures = 0;

        await Parallel.ForEachAsync(
            selectedIds,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = options.Value.MaxConcurrency,
                CancellationToken = cancellationToken
            },
            async (id, token) =>
            {
                try
                {
                    var item = await client.GetStoryAsync(id, token);
                    if (item is { Type: "story", Deleted: false, Dead: false })
                    {
                        stories.Add(Map(item));
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Best-effort per item: a single bad item must not fail the request.
                    Interlocked.Increment(ref failures);
                    logger.LogWarning(ex, "Failed to retrieve story {StoryId}; skipping", id);
                }
            });

        if (stories.IsEmpty && selectedIds.Length > 0)
        {
            return Result.Fail<ImmutableArray<BestStoryResponse>>(
                new UpstreamUnavailableError("The Hacker News API is unavailable; no stories could be retrieved."));
        }

        if (failures > 0)
        {
            logger.LogWarning("{FailureCount} of {RequestedCount} stories failed to load", failures, selectedIds.Length);
        }

        // Re-sort by score: the upstream list is rank-ordered, not strictly score-sorted.
        var sorted = stories
            .OrderByDescending(s => s.Score)
            .ToImmutableArray();

        return Result.Ok(sorted);
    }

    private static BestStoryResponse Map(HackerNewsItem item) => new(
        Title: item.Title ?? string.Empty,
        Uri: item.Url ?? string.Empty,
        PostedBy: item.By ?? string.Empty,
        Time: DateTimeOffset.FromUnixTimeSeconds(item.Time),
        Score: item.Score,
        CommentCount: item.Descendants);
}
