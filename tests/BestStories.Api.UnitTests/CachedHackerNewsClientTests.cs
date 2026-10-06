using BestStories.Api.Clients.HackerNews;
using BestStories.Api.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace BestStories.Api.UnitTests;

public class CachedHackerNewsClientTests : IDisposable
{
    private readonly IHackerNewsClient _inner = Substitute.For<IHackerNewsClient>();
    private readonly ServiceProvider _provider;
    private readonly CachedHackerNewsClient _sut;

    public CachedHackerNewsClientTests()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        _provider = services.BuildServiceProvider();
        _sut = new CachedHackerNewsClient(
            _inner,
            _provider.GetRequiredService<HybridCache>(),
            Options.Create(new CacheOptions()));
    }

    [Fact]
    public async Task Best_story_ids_are_served_from_cache_on_second_call()
    {
        _inner.GetBestStoryIdsAsync(Arg.Any<CancellationToken>())
            .Returns(new ValueTask<int[]>([1, 2, 3]));

        var first = await _sut.GetBestStoryIdsAsync(CancellationToken.None);
        var second = await _sut.GetBestStoryIdsAsync(CancellationToken.None);

        second.Should().Equal(first);
        await _inner.Received(1).GetBestStoryIdsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Story_items_are_cached_per_id()
    {
        _inner.GetStoryAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<HackerNewsItem?>(new HackerNewsItem { Id = 7, Type = "story" }));

        await _sut.GetStoryAsync(7, CancellationToken.None);
        await _sut.GetStoryAsync(7, CancellationToken.None);
        await _sut.GetStoryAsync(8, CancellationToken.None);

        await _inner.Received(1).GetStoryAsync(7, Arg.Any<CancellationToken>());
        await _inner.Received(1).GetStoryAsync(8, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Concurrent_misses_collapse_into_a_single_upstream_call()
    {
        var calls = 0;
        _inner.GetBestStoryIdsAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Fetch());

        async ValueTask<int[]> Fetch()
        {
            Interlocked.Increment(ref calls);
            await Task.Delay(50);
            return [1];
        }

        var pending = Enumerable.Range(0, 10)
            .Select(_ => _sut.GetBestStoryIdsAsync(CancellationToken.None).AsTask())
            .ToArray();
        await Task.WhenAll(pending);

        calls.Should().Be(1);
    }

    public void Dispose() => _provider.Dispose();
}
