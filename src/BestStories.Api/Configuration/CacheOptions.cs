using System.ComponentModel.DataAnnotations;

namespace BestStories.Api.Configuration;

public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    [Range(1, 3600)]
    public int StoryIdsTtlSeconds { get; set; } = 120;

    [Range(1, 3600)]
    public int StoryItemTtlSeconds { get; set; } = 300;
}
