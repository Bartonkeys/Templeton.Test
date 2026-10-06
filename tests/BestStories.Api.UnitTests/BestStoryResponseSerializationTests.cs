using System.Text.Json;
using BestStories.Api.Common.Serialization;
using BestStories.Api.Features.BestStories;
using FluentAssertions;
using Xunit;

namespace BestStories.Api.UnitTests;

public class BestStoryResponseSerializationTests
{
    [Fact]
    public void Serialises_to_the_exact_shape_required_by_the_spec()
    {
        var response = new BestStoryResponse(
            Title: "A uBlock Origin update was rejected from the Chrome Web Store",
            Uri: "https://github.com/uBlockOrigin/uBlock-issues/issues/745",
            PostedBy: "ismaildonmez",
            Time: DateTimeOffset.FromUnixTimeSeconds(1570887781),
            Score: 1716,
            CommentCount: 572);

        var json = JsonSerializer.Serialize(response, AppJsonSerializerContext.Default.BestStoryResponse);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        root.GetProperty("title").GetString().Should().Be("A uBlock Origin update was rejected from the Chrome Web Store");
        root.GetProperty("uri").GetString().Should().Be("https://github.com/uBlockOrigin/uBlock-issues/issues/745");
        root.GetProperty("postedBy").GetString().Should().Be("ismaildonmez");
        root.GetProperty("time").GetString().Should().Be("2019-10-12T13:43:01+00:00");
        root.GetProperty("score").GetInt32().Should().Be(1716);
        root.GetProperty("commentCount").GetInt32().Should().Be(572);
        root.EnumerateObject().Count().Should().Be(6);
    }
}
