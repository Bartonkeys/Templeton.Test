using BestStories.Api.Clients.HackerNews;
using BestStories.Api.Configuration;
using BestStories.Api.Features.BestStories;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace BestStories.Api.UnitTests;

public class BestStoriesServiceTests
{
    private readonly IHackerNewsClient _client = Substitute.For<IHackerNewsClient>();
    private readonly BestStoriesService _sut;

    public BestStoriesServiceTests()
    {
        _sut = new BestStoriesService(
            _client,
            Options.Create(new HackerNewsOptions { MaxConcurrency = 4 }),
            NullLogger<BestStoriesService>.Instance);
    }

    [Fact]
    public async Task Returns_stories_sorted_by_score_descending()
    {
        _client.GetBestStoryIdsAsync(Arg.Any<CancellationToken>())
            .Returns(new ValueTask<int[]>([1, 2, 3]));
        _client.GetStoryAsync(1, Arg.Any<CancellationToken>()).Returns(Item(1, score: 10));
        _client.GetStoryAsync(2, Arg.Any<CancellationToken>()).Returns(Item(2, score: 30));
        _client.GetStoryAsync(3, Arg.Any<CancellationToken>()).Returns(Item(3, score: 20));

        var result = await _sut.GetBestStoriesAsync(3, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(s => s.Score).Should().Equal(30, 20, 10);
    }

    [Fact]
    public async Task Fetches_only_the_first_n_ids_from_the_ranked_list()
    {
        _client.GetBestStoryIdsAsync(Arg.Any<CancellationToken>())
            .Returns(new ValueTask<int[]>([10, 20, 30, 40]));
        _client.GetStoryAsync(10, Arg.Any<CancellationToken>()).Returns(Item(10, 5));
        _client.GetStoryAsync(20, Arg.Any<CancellationToken>()).Returns(Item(20, 6));

        var result = await _sut.GetBestStoriesAsync(2, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        await _client.DidNotReceive().GetStoryAsync(30, Arg.Any<CancellationToken>());
        await _client.DidNotReceive().GetStoryAsync(40, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Skips_deleted_dead_and_non_story_items()
    {
        _client.GetBestStoryIdsAsync(Arg.Any<CancellationToken>())
            .Returns(new ValueTask<int[]>([1, 2, 3, 4]));
        _client.GetStoryAsync(1, Arg.Any<CancellationToken>())
            .Returns(new HackerNewsItem { Id = 1, Score = 5, Type = "story", Deleted = true });
        _client.GetStoryAsync(2, Arg.Any<CancellationToken>())
            .Returns(new HackerNewsItem { Id = 2, Score = 6, Type = "story", Dead = true });
        _client.GetStoryAsync(3, Arg.Any<CancellationToken>())
            .Returns(new HackerNewsItem { Id = 3, Score = 7, Type = "comment" });
        _client.GetStoryAsync(4, Arg.Any<CancellationToken>())
            .Returns(Item(4, 8));

        var result = await _sut.GetBestStoriesAsync(4, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(s => s.Score == 8);
    }

    [Fact]
    public async Task Maps_item_fields_to_the_response_shape()
    {
        const long unixTime = 1570887781; // 2019-10-12T13:43:01+00:00
        _client.GetBestStoryIdsAsync(Arg.Any<CancellationToken>())
            .Returns(new ValueTask<int[]>([1]));
        _client.GetStoryAsync(1, Arg.Any<CancellationToken>()).Returns(new HackerNewsItem
        {
            Id = 1,
            By = "ismaildonmez",
            Descendants = 572,
            Score = 1716,
            Time = unixTime,
            Title = "A uBlock Origin update was rejected from the Chrome Web Store",
            Type = "story",
            Url = "https://github.com/uBlockOrigin/uBlock-issues/issues/745"
        });

        var result = await _sut.GetBestStoriesAsync(1, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value[0].Should().BeEquivalentTo(new
        {
            Title = "A uBlock Origin update was rejected from the Chrome Web Store",
            Uri = "https://github.com/uBlockOrigin/uBlock-issues/issues/745",
            PostedBy = "ismaildonmez",
            Time = DateTimeOffset.FromUnixTimeSeconds(unixTime),
            Score = 1716,
            CommentCount = 572
        });
    }

    [Fact]
    public async Task Tolerates_individual_item_failures_and_returns_partial_results()
    {
        _client.GetBestStoryIdsAsync(Arg.Any<CancellationToken>())
            .Returns(new ValueTask<int[]>([1, 2]));
        _client.GetStoryAsync(1, Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("boom"));
        _client.GetStoryAsync(2, Arg.Any<CancellationToken>()).Returns(Item(2, 9));

        var result = await _sut.GetBestStoriesAsync(2, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(s => s.Score == 9);
    }

    [Fact]
    public async Task Fails_when_the_id_list_cannot_be_retrieved()
    {
        _client.GetBestStoryIdsAsync(Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("network down"));

        var result = await _sut.GetBestStoriesAsync(3, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e is UpstreamUnavailableError);
    }

    [Fact]
    public async Task Fails_when_all_items_fail_to_load()
    {
        _client.GetBestStoryIdsAsync(Arg.Any<CancellationToken>())
            .Returns(new ValueTask<int[]>([1, 2]));
        _client.GetStoryAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("boom"));

        var result = await _sut.GetBestStoriesAsync(2, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e is UpstreamUnavailableError);
    }

    [Fact]
    public async Task Returns_empty_result_when_the_best_list_is_empty()
    {
        _client.GetBestStoryIdsAsync(Arg.Any<CancellationToken>())
            .Returns(new ValueTask<int[]>([]));

        var result = await _sut.GetBestStoriesAsync(5, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    private static HackerNewsItem Item(int id, int score) =>
        new()
        {
            Id = id,
            By = "user",
            Descendants = 1,
            Score = score,
            Time = 1_700_000_000,
            Title = $"Story {id}",
            Type = "story",
            Url = "https://example.com"
        };
}
