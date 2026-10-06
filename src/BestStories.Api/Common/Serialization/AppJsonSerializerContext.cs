using System.Collections.Immutable;
using System.Text.Json.Serialization;
using BestStories.Api.Clients.HackerNews;
using BestStories.Api.Features.BestStories;

namespace BestStories.Api.Common.Serialization;

/// <summary>
/// Source-generated JSON metadata — avoids reflection-based serialization, keeps
/// allocations low and leaves the door open to Native AOT publishing.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(int[]))]
[JsonSerializable(typeof(HackerNewsItem))]
[JsonSerializable(typeof(ImmutableArray<BestStoryResponse>))]
[JsonSerializable(typeof(BestStoryResponse))]
public partial class AppJsonSerializerContext : JsonSerializerContext;
