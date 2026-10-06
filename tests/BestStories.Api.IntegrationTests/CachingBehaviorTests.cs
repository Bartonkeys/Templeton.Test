using System.Net;
using FluentAssertions;
using Xunit;

namespace BestStories.Api.IntegrationTests;

/// <summary>
/// Proves the core spec requirement: high request volumes must not overload the
/// upstream Hacker News API. Verified by counting calls the WireMock container
/// actually received via its admin API.
/// </summary>
[Collection(HackerNewsStubCollection.Name)]
public class CachingBehaviorTests(HackerNewsStubFixture fixture)
{
    private const string Endpoint = "/api/v1/beststories";

    [Fact]
    public async Task Repeated_requests_are_served_from_cache()
    {
        using var client = fixture.CreateClient();
        await fixture.ResetUpstreamRequestLogAsync();

        for (var i = 0; i < 3; i++)
        {
            var response = await client.GetAsync($"{Endpoint}?n=3");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        (await fixture.CountUpstreamRequestsAsync("/v0/beststories.json")).Should().Be(1);
        (await fixture.CountUpstreamRequestsAsync("/v0/item/101.json")).Should().Be(1);
        (await fixture.CountUpstreamRequestsAsync("/v0/item/102.json")).Should().Be(1);
        (await fixture.CountUpstreamRequestsAsync("/v0/item/103.json")).Should().Be(1);
    }

    [Fact]
    public async Task Concurrent_requests_do_not_stampede_the_upstream()
    {
        using var client = fixture.CreateClient();
        await fixture.ResetUpstreamRequestLogAsync();

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 20).Select(_ => client.GetAsync($"{Endpoint}?n=3")));

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
        (await fixture.CountUpstreamRequestsAsync("/v0/beststories.json")).Should().Be(1);
    }
}
