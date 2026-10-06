using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using BestStories.Api.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace BestStories.Api.Auth;

public sealed class ApiKeyAuthSchemeOptions : AuthenticationSchemeOptions;

/// <summary>
/// X-Api-Key header authentication. Config-gated: when Authentication:ApiKey is not
/// configured, every request is treated as authenticated so the API stays friction-free
/// to run locally — set the key (user-secrets/env var) to enforce 401s.
/// The key comparison is constant-time to avoid leaking the key via timing.
/// </summary>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<ApiKeyOptions> authentication)
    : AuthenticationHandler<ApiKeyAuthSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var configuredKey = authentication.Value.ApiKey;
        if (string.IsNullOrEmpty(configuredKey))
        {
            Logger.LogDebug("No API key configured; authentication is disabled");
            return Task.FromResult(AuthenticateResult.Success(CreateTicket("anonymous")));
        }

        if (!Request.Headers.TryGetValue(HeaderName, out var header) || header.Count != 1)
        {
            return Task.FromResult(AuthenticateResult.Fail($"Missing {HeaderName} header."));
        }

        var providedKey = header.ToString();
        var isValid = providedKey.Length == configuredKey.Length
            && CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(providedKey),
                Encoding.UTF8.GetBytes(configuredKey));

        return Task.FromResult(isValid
            ? AuthenticateResult.Success(CreateTicket("apikey-client"))
            : AuthenticateResult.Fail("Invalid API key."));
    }

    private AuthenticationTicket CreateTicket(string name)
    {
        var identity = new ClaimsIdentity(Scheme.Name);
        identity.AddClaim(new Claim(ClaimTypes.Name, name));
        return new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
    }
}
