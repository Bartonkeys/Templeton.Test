using FluentResults;

namespace BestStories.Api.Features.BestStories;

/// <summary>Upstream Hacker News API unreachable or consistently failing.</summary>
public sealed class UpstreamUnavailableError : Error
{
    public UpstreamUnavailableError(string message, Exception? causedBy = null) : base(message)
    {
        Metadata[ResultHttpMetadata.StatusCodeKey] = StatusCodes.Status503ServiceUnavailable;
        if (causedBy is not null)
        {
            CausedBy(causedBy);
        }
    }
}

/// <summary>Upstream returned a payload we could not interpret.</summary>
public sealed class UpstreamInvalidResponseError : Error
{
    public UpstreamInvalidResponseError(string message, Exception? causedBy = null) : base(message)
    {
        Metadata[ResultHttpMetadata.StatusCodeKey] = StatusCodes.Status502BadGateway;
        if (causedBy is not null)
        {
            CausedBy(causedBy);
        }
    }
}

public static class ResultHttpMetadata
{
    public const string StatusCodeKey = "httpStatus";
}
