using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace BestStories.Api.IntegrationTests;

[Collection(HackerNewsStubCollection.Name)]
public class ResilienceTests(HackerNewsStubFixture fixture)
{
    private const string Endpoint = "/api/v1/beststories";

    [Fact]
    public async Task Upstream_failures_are_retried_then_surface_a_503_problem_details()
    {
        using var client = fixture.CreateClient(new Dictionary<string, string?>
        {
            ["HackerNews:BaseUrl"] = fixture.StubBaseUrl("v0-down")
        });
        await fixture.ResetUpstreamRequestLogAsync();

        var response = await client.GetAsync($"{Endpoint}?n=3");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("status").GetInt32().Should().Be(503);

        // The resilience handler retries — the stub should have seen multiple attempts.
        (await fixture.CountUpstreamRequestsAsync("/v0-down/beststories.json"))
            .Should().BeGreaterThan(1);
    }
}
