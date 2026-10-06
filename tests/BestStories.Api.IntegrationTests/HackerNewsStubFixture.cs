using BestStories.Api.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using WireMock.Client;
using WireMock.Client.Extensions;
using WireMock.Net.Testcontainers;
using Xunit;

namespace BestStories.Api.IntegrationTests;

/// <summary>
/// Shared WireMock.Net container standing in for the Hacker News API.
/// One container per xUnit collection; each test builds its own
/// WebApplicationFactory so every test gets a fresh app instance (fresh cache).
/// Requires a running Docker daemon.
/// </summary>
public sealed class HackerNewsStubFixture : IAsyncLifetime
{
    public const string ApiKey = "test-api-key";

    private readonly WireMockContainer _container = new WireMockContainerBuilder()
        .WithMappings(Path.Combine(AppContext.BaseDirectory, "wiremock", "mappings"))
        .WithAutoRemove(true)
        .WithCleanUp(true)
        .Build();

    private IWireMockAdminApi? _admin;

    public string PublicUrl => _container.GetPublicUrl();

    public string StubBaseUrl(string prefix = "v0") =>
        $"{PublicUrl.TrimEnd('/')}/{prefix}/";

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _admin = _container.CreateWireMockAdminClient();
        await _admin.WaitForHealthAsync(60);
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public WebApplicationFactory<Program> CreateFactory(
        IEnumerable<KeyValuePair<string, string?>>? configOverrides = null)
    {
        var settings = new Dictionary<string, string?>
        {
            [$"{HackerNewsOptions.SectionName}:BaseUrl"] = StubBaseUrl(),
            ["Authentication:ApiKey"] = ApiKey
        };

        foreach (var (key, value) in configOverrides ?? [])
        {
            settings[key] = value;
        }

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder
                .UseEnvironment("Development")
                .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(settings)));
    }

    public HttpClient CreateClient(
        IEnumerable<KeyValuePair<string, string?>>? configOverrides = null)
    {
        var client = CreateFactory(configOverrides).CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
        return client;
    }

    /// <summary>Counts requests the WireMock container received for a given path.</summary>
    public async Task<int> CountUpstreamRequestsAsync(string path)
    {
        var entries = await _admin!.GetRequestsAsync();
        return entries.Count(e => e.Request?.Path == path);
    }

    public Task ResetUpstreamRequestLogAsync() => _admin!.ResetRequestsAsync();
}

[CollectionDefinition(Name)]
public sealed class HackerNewsStubCollection : ICollectionFixture<HackerNewsStubFixture>
{
    public const string Name = "HackerNewsStub";
}
