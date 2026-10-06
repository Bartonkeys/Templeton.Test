using System.ComponentModel.DataAnnotations;

namespace BestStories.Api.Configuration;

public sealed class HackerNewsOptions
{
    public const string SectionName = "HackerNews";

    [Required]
    public Uri BaseUrl { get; set; } = new("https://hacker-news.firebaseio.com/v0/");

    [Range(1, 1000)]
    public int MaxStoryCount { get; set; } = 200;

    [Range(1, 64)]
    public int MaxConcurrency { get; set; } = 16;
}
