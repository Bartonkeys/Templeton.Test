using System.Threading.RateLimiting;
using BestStories.Api.Auth;
using BestStories.Api.Clients.HackerNews;
using BestStories.Api.Common;
using BestStories.Api.Common.Observability;
using BestStories.Api.Configuration;
using BestStories.Api.Features.BestStories;
using BestStories.Api.Health;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// --- Options: bound, validated, fail-fast on startup ---
builder.Services.AddOptions<HackerNewsOptions>()
    .Bind(builder.Configuration.GetSection(HackerNewsOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<CacheOptions>()
    .Bind(builder.Configuration.GetSection(CacheOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<RateLimitingOptions>()
    .Bind(builder.Configuration.GetSection(RateLimitingOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<ApiKeyOptions>()
    .Bind(builder.Configuration.GetSection(ApiKeyOptions.SectionName));

// --- Observability ---
builder.Services.AddSingleton<HackerNewsMetrics>();

// --- Caching (in-process L1; registering IDistributedCache upgrades to L2) ---
builder.Services.AddHybridCache();

// --- Hacker News client: resilience on the handler, cross-cutting concerns as decorators ---
builder.Services.AddHttpClient<IHackerNewsClient, HackerNewsClient>()
    .ConfigureHttpClient((sp, client) =>
    {
        client.BaseAddress = sp.GetRequiredService<IOptions<HackerNewsOptions>>().Value.BaseUrl;
        client.DefaultRequestHeaders.UserAgent.ParseAdd("BestStories.Api/1.0");
    })
    .AddStandardResilienceHandler();

builder.Services.Decorate<IHackerNewsClient, InstrumentedHackerNewsClient>();
builder.Services.Decorate<IHackerNewsClient, CachedHackerNewsClient>(); // outermost

// --- Feature services ---
builder.Services.AddScoped<BestStoriesService>();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// --- Authentication / authorisation (API key, config-gated) ---
builder.Services.AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<ApiKeyAuthSchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationHandler.SchemeName, _ => { });
builder.Services.AddAuthorization();

// --- Rate limiting ---
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(BestStoriesEndpoint.RateLimitPolicy, httpContext =>
    {
        var limits = httpContext.RequestServices
            .GetRequiredService<IOptionsMonitor<RateLimitingOptions>>().CurrentValue;
        var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limits.PermitLimit,
            Window = TimeSpan.FromSeconds(limits.WindowSeconds),
            QueueLimit = 0
        });
    });
});

// --- OpenAPI document ---
builder.Services.AddOpenApi(options =>
{
    // ASP.NET Core emits OpenAPI 3.1 where query params are typed "integer|string"
    // (they arrive as strings). Swagger UI's client-side required-field check fails
    // on union types, so collapse them back to a plain integer with bounds.
    options.AddOperationTransformer((operation, context, _) =>
    {
        var max = context.ApplicationServices
            .GetRequiredService<IOptions<HackerNewsOptions>>().Value.MaxStoryCount;

        foreach (var parameter in operation.Parameters ?? [])
        {
            if (parameter.Schema is OpenApiSchema schema
                && schema.Type == (JsonSchemaType.Integer | JsonSchemaType.String))
            {
                schema.Type = JsonSchemaType.Integer;
                schema.Pattern = null;
                schema.Minimum = "1";
                schema.Maximum = max.ToString();
            }
        }

        return Task.CompletedTask;
    });

    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "Best Stories API",
            Version = "v1",
            Description = "Returns the best n Hacker News stories, ordered by descending score."
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[ApiKeyAuthenticationHandler.SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = ApiKeyAuthenticationHandler.HeaderName,
            Description = $"Provide your API key in the '{ApiKeyAuthenticationHandler.HeaderName}' header. " +
                          "Authentication is disabled when Authentication:ApiKey is not configured."
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(ApiKeyAuthenticationHandler.SchemeName, document)] = []
        });

        return Task.CompletedTask;
    });
});

// --- Error handling + health checks ---
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<HackerNewsHealthCheck>("hackernews", failureStatus: HealthStatus.Degraded, tags: ["ready"]);

var app = builder.Build();

app.UseExceptionHandler();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = r => r.Tags.Contains("live") })
    .AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") })
    .AllowAnonymous();

app.MapBestStoriesEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapGet("/", () => Results.Redirect("/swagger"))
        .ExcludeFromDescription()
        .AllowAnonymous();
    app.MapOpenApi().AllowAnonymous();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "Best Stories API v1"));
}

app.Run();

public partial class Program;
