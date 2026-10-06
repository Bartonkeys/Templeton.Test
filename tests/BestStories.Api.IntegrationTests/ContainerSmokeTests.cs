using System.Net;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using DotNet.Testcontainers.Networks;
using FluentAssertions;
using WireMock.Net.Testcontainers;
using Xunit;

namespace BestStories.Api.IntegrationTests;

/// <summary>
/// Builds the real Dockerfile and runs it alongside the WireMock stub on a shared
/// Docker network — an end-to-end check of the shipped deployment artifact.
/// Slow (image build); skip with: dotnet test --filter "Category!=Smoke"
/// </summary>
[Trait("Category", "Smoke")]
public class ContainerSmokeTests : IAsyncLifetime
{
    private readonly INetwork _network = new NetworkBuilder()
        .WithName($"beststories-{Guid.NewGuid():N}")
        .Build();

    private WireMockContainer _stub = null!;
    private IFutureDockerImage _image = null!;
    private IContainer _api = null!;

    public async Task InitializeAsync()
    {
        var mappingsPath = Path.Combine(AppContext.BaseDirectory, "wiremock", "mappings");

        await _network.CreateAsync();

        _stub = new WireMockContainerBuilder()
            .WithMappings(mappingsPath)
            .WithNetwork(_network)
            .WithNetworkAliases("hackernews-stub")
            .WithAutoRemove(true)
            .Build();
        await _stub.StartAsync();

        _image = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(CommonDirectoryPath.GetGitDirectory().DirectoryPath)
            .WithDockerfile("Dockerfile")
            .WithCleanUp(true)
            .Build();
        await _image.CreateAsync();

        _api = new ContainerBuilder(_image)
            .WithNetwork(_network)
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Production")
            .WithEnvironment("HackerNews__BaseUrl", "http://hackernews-stub/v0/")
            .WithPortBinding(8080, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r.ForPort(8080).ForPath("/health")))
            .Build();
        await _api.StartAsync();
    }

    [Fact]
    public async Task The_built_container_serves_best_stories()
    {
        var endpoint = new UriBuilder("http", _api.Hostname, _api.GetMappedPublicPort(8080))
        {
            Path = "api/v1/beststories",
            Query = "n=3"
        }.Uri;

        using var client = new HttpClient();
        var response = await client.GetAsync(endpoint);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"score\":300");
    }

    public async Task DisposeAsync()
    {
        if (_api is not null)
        {
            await _api.DisposeAsync();
        }
        if (_stub is not null)
        {
            await _stub.DisposeAsync();
        }
        await _network.DisposeAsync();
    }
}
