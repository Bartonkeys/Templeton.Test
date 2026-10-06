namespace BestStories.Api.Clients.HackerNews;

public interface IHackerNewsClient
{
    /// <summary>Ids of the current "best" stories, ordered best-first by the HN API.</summary>
    ValueTask<int[]> GetBestStoryIdsAsync(CancellationToken cancellationToken);

    /// <summary>Details for a single item. Null when the item does not exist.</summary>
    ValueTask<HackerNewsItem?> GetStoryAsync(int id, CancellationToken cancellationToken);
}
