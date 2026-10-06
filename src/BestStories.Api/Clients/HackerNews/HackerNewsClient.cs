using System.Net;
using System.Net.Http.Json;
using BestStories.Api.Common.Serialization;

namespace BestStories.Api.Clients.HackerNews;

/// <summary>
/// Typed HttpClient for the Hacker News Firebase API. Resilience (retries, circuit
/// breaker, timeouts) is applied at the handler level via AddStandardResilienceHandler.
/// </summary>
public sealed class HackerNewsClient(HttpClient httpClient) : IHackerNewsClient
{
    public async ValueTask<int[]> GetBestStoryIdsAsync(CancellationToken cancellationToken)
    {
        var ids = await httpClient.GetFromJsonAsync(
            "beststories.json",
            AppJsonSerializerContext.Default.Int32Array,
            cancellationToken);

        return ids ?? [];
    }

    public async ValueTask<HackerNewsItem?> GetStoryAsync(int id, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync($"item/{id}.json", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
            AppJsonSerializerContext.Default.HackerNewsItem,
            cancellationToken);
    }
}
