using BestStories.Api.Configuration;
using BestStories.Api.Features.BestStories;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Options;
using Xunit;

namespace BestStories.Api.UnitTests;

public class GetBestStoriesRequestValidatorTests
{
    private const int Max = 200;
    private readonly GetBestStoriesRequestValidator _validator = new(
        Options.Create(new HackerNewsOptions { MaxStoryCount = Max }));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(Max + 1)]
    [InlineData(int.MaxValue)]
    public void Rejects_out_of_range_values(int n)
    {
        var result = _validator.TestValidate(new GetBestStoriesRequest { N = n });

        result.ShouldHaveValidationErrorFor(x => x.N);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(Max)]
    [InlineData(Max / 2)]
    public void Accepts_values_in_range(int n)
    {
        var result = _validator.TestValidate(new GetBestStoriesRequest { N = n });

        result.ShouldNotHaveValidationErrorFor(x => x.N);
    }
}
