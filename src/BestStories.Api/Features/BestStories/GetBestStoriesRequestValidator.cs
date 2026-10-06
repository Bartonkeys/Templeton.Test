using BestStories.Api.Configuration;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace BestStories.Api.Features.BestStories;

public sealed class GetBestStoriesRequestValidator : AbstractValidator<GetBestStoriesRequest>
{
    public GetBestStoriesRequestValidator(IOptions<HackerNewsOptions> options)
    {
        var max = options.Value.MaxStoryCount;

        RuleFor(x => x.N)
            .InclusiveBetween(1, max)
            .WithMessage($"n must be between 1 and {max}.");
    }
}
