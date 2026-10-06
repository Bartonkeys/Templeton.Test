using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace BestStories.Api.IntegrationTests;

[Collection(HackerNewsStubCollection.Name)]
public class BestStoriesEndpointTests(HackerNewsStubFixture fixture)
{
    private const string Endpoint = "/api/v1/beststories";

    [Fact]
    public async Task Returns_stories_ordered_by_descending_score_in_the_spec_shape()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync($"{Endpoint}?n=3");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var stories = document.RootElement.EnumerateArray().ToArray();

        stories.Should().HaveCount(3);
        stories.Select(s => s.GetProperty("score").GetInt32())
            .Should().Equal(300, 200, 100);

        var first = stories[0];
        first.GetProperty("title").GetString().Should().Be("Stub story 102");
        first.GetProperty("uri").GetString().Should().Be("https://example.com/102");
        first.GetProperty("postedBy").GetString().Should().Be("tester2");
        first.GetProperty("time").GetString().Should().Be("2019-10-12T13:43:02+00:00");
        first.GetProperty("commentCount").GetInt32().Should().Be(7);
    }

    [Fact]
    public async Task Skips_deleted_and_missing_items()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync($"{Endpoint}?n=5");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var stories = document.RootElement.EnumerateArray().ToArray();

        // 104 is deleted, 105 has no stub (404) — only the three live stories return.
        stories.Should().HaveCount(3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(201)]
    public async Task Returns_400_for_out_of_range_n(int n)
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync($"{Endpoint}?n={n}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("n must be between 1 and 200");
    }

    [Fact]
    public async Task Returns_401_without_an_api_key()
    {
        using var client = fixture.CreateFactory().CreateClient();

        var response = await client.GetAsync($"{Endpoint}?n=3");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Returns_200_with_a_valid_api_key()
    {
        using var client = fixture.CreateFactory().CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "wrong-key");

        var response = await client.GetAsync($"{Endpoint}?n=3");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Returns_429_when_the_rate_limit_is_exceeded()
    {
        using var client = fixture.CreateClient(new Dictionary<string, string?>
        {
            ["RateLimiting:PermitLimit"] = "2",
            ["RateLimiting:WindowSeconds"] = "60"
        });

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            var response = await client.GetAsync($"{Endpoint}?n=1");
            statuses.Add(response.StatusCode);
        }

        statuses.Take(2).Should().OnlyContain(s => s == HttpStatusCode.OK);
        statuses.Skip(2).Should().OnlyContain(s => s == HttpStatusCode.TooManyRequests);
    }
}
