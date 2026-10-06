using BestStories.Api.Configuration;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace BestStories.Api.Clients.HackerNews;

/// <summary>
/// Decorator: caches upstream responses in HybridCache. HybridCache provides
/// stampede protection — concurrent misses on the same key collapse into a single
/// factory call — which is the primary defence against overloading the HN API.
/// Registered outermost so inner decorators only see genuine upstream traffic.
/// </summary>
public sealed class CachedHackerNewsClient(
    IHackerNewsClient inner,
    HybridCache cache,
    IOptions<CacheOptions> options) : IHackerNewsClient
{
    private const string BestStoryIdsKey = "hn:beststories:ids";
    private static readonly string[] CacheTags = ["hacker-news"];

    private readonly HybridCacheEntryOptions _idsEntryOptions = new()
    {
        Expiration = TimeSpan.FromSeconds(options.Value.StoryIdsTtlSeconds),
        LocalCacheExpiration = TimeSpan.FromSeconds(options.Value.StoryIdsTtlSeconds)
    };

    private readonly HybridCacheEntryOptions _itemEntryOptions = new()
    {
        Expiration = TimeSpan.FromSeconds(options.Value.StoryItemTtlSeconds),
        LocalCacheExpiration = TimeSpan.FromSeconds(options.Value.StoryItemTtlSeconds)
    };

    public ValueTask<int[]> GetBestStoryIdsAsync(CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(
            BestStoryIdsKey,
            inner,
            static (client, token) => client.GetBestStoryIdsAsync(token),
            _idsEntryOptions,
            CacheTags,
            cancellationToken);

    public ValueTask<HackerNewsItem?> GetStoryAsync(int id, CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(
            $"hn:item:{id}",
            (inner, id),
            static (state, token) => state.inner.GetStoryAsync(state.id, token),
            _itemEntryOptions,
            CacheTags,
            cancellationToken);
}
