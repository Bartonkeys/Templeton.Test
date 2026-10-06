using Microsoft.AspNetCore.Mvc;

namespace BestStories.Api.Features.BestStories;

/// <summary>Query contract for GET /api/v1/beststories?n={count}.</summary>
public sealed record GetBestStoriesRequest
{
    /// <summary>Number of stories to return.</summary>
    [FromQuery(Name = "n")]
    public int N { get; init; }
}
