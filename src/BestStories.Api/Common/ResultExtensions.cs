using FluentResults;

namespace BestStories.Api.Common;

public static class ResultExtensions
{
    /// <summary>
    /// Maps a failed FluentResults <see cref="ResultBase"/> to an RFC 7807 ProblemDetails
    /// response, using the "httpStatus" metadata key carried by our error types.
    /// </summary>
    public static IResult ToProblemResult(this ResultBase result, HttpContext httpContext)
    {
        var error = result.Errors.FirstOrDefault();
        var statusCode = ResolveStatusCode(error);

        return Results.Problem(
            statusCode: statusCode,
            title: statusCode switch
            {
                StatusCodes.Status502BadGateway => "Upstream invalid response",
                StatusCodes.Status503ServiceUnavailable => "Upstream service unavailable",
                _ => "An unexpected error occurred"
            },
            detail: error?.Message,
            extensions: new Dictionary<string, object?> { ["traceId"] = httpContext.TraceIdentifier });
    }

    private static int ResolveStatusCode(IError? error) =>
        error?.Metadata.TryGetValue("httpStatus", out var value) == true && value is int code
            ? code
            : StatusCodes.Status500InternalServerError;
}
