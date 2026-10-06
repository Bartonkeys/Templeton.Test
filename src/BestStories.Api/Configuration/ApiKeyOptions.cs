namespace BestStories.Api.Configuration;

public sealed class ApiKeyOptions
{
    public const string SectionName = "Authentication";

    /// <summary>
    /// API key expected in the X-Api-Key header. When null or empty, authentication
    /// is disabled and all requests are treated as authenticated (local development mode).
    /// </summary>
    public string? ApiKey { get; set; }
}
