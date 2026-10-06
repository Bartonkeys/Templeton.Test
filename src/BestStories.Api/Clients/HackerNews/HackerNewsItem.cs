namespace BestStories.Api.Clients.HackerNews;

/// <summary>
/// Subset of the Hacker News item payload we care about.
/// https://github.com/HackerNews/API
/// </summary>
public sealed record HackerNewsItem
{
    public int Id { get; init; }
    public string? By { get; init; }
    public int Descendants { get; init; }
    public int Score { get; init; }
    public long Time { get; init; }
    public string? Title { get; init; }
    public string? Type { get; init; }
    public string? Url { get; init; }
    public bool Deleted { get; init; }
    public bool Dead { get; init; }
}
