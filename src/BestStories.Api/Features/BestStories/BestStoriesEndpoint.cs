using System.Collections.Immutable;
using BestStories.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace BestStories.Api.Features.BestStories;

public static class BestStoriesEndpoint
{
    public const string RateLimitPolicy = "stories";

    public static RouteGroupBuilder MapBestStoriesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1")
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitPolicy);

        group.MapGet("/beststories", HandleAsync)
            .AddEndpointFilter<ValidationEndpointFilter<GetBestStoriesRequest>>()
            .WithName("GetBestStories")
            .WithSummary("Best Hacker News stories")
            .WithDescription("Returns the best n Hacker News stories, ordered by descending score.")
            .Produces<ImmutableArray<BestStoryResponse>>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return group;
    }

    private static async Task<IResult> HandleAsync(
        [AsParameters] GetBestStoriesRequest request,
        BestStoriesService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await service.GetBestStoriesAsync(request.N, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.ToProblemResult(httpContext);
    }
}
