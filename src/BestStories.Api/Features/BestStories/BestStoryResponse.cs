namespace BestStories.Api.Features.BestStories;

/// <summary>
/// Response shape required by the coding-test spec. System.Text.Json web defaults
/// serialise to camelCase: title, uri, postedBy, time, score, commentCount.
/// </summary>
public sealed record BestStoryResponse(
    string Title,
    string Uri,
    string PostedBy,
    DateTimeOffset Time,
    int Score,
    int CommentCount);
