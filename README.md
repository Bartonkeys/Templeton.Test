# Best Stories API

ASP.NET Core 10 API that returns the best *n* stories from the
[Hacker News API](https://github.com/HackerNews/API), ordered by score descending.
The interesting constraint in the spec was serving a lot of requests without
hammering the upstream API, so that's where most of the effort went.

```
GET /api/v1/beststories?n=10
```

```json
[
  {
    "title": "A uBlock Origin update was rejected from the Chrome Web Store",
    "uri": "https://github.com/uBlockOrigin/uBlock-issues/issues/745",
    "postedBy": "ismaildonmez",
    "time": "2019-10-12T13:43:01+00:00",
    "score": 1716,
    "commentCount": 572
  }
]
```

## Prerequisites

- .NET 10 SDK (pinned in `global.json`, `rollForward: latestFeature`)
- Docker Desktop, but only for the integration tests and running the container

## Running it

```bash
dotnet run --project src/BestStories.Api
```

Swagger UI is on <https://localhost:7031/swagger> in Development, or just:

```bash
curl "http://localhost:5009/api/v1/beststories?n=5"
```

With Docker:

```bash
docker compose up --build   # http://localhost:8080
```

## Tests

```bash
dotnet test                                        # everything
dotnet test --filter "Category!=Smoke"             # skip the dockerized end to end test
dotnet test tests/BestStories.Api.UnitTests        # no Docker needed
```

The integration tests use Testcontainers to spin up a real WireMock.Net container
as the Hacker News stub. I wanted the real `HttpClient`, resilience pipeline and
JSON serialization exercised over actual HTTP rather than a mocked handler. Bonus:
the WireMock request log lets the tests count upstream calls, which is how the
caching tests actually prove we don't overload HN rather than just asserting it.

## Configuration

| Key | Default | What it does |
|---|---|---|
| `HackerNews:BaseUrl` | `https://hacker-news.firebaseio.com/v0/` | Upstream API |
| `HackerNews:MaxStoryCount` | `200` | Upper bound on `n` |
| `HackerNews:MaxConcurrency` | `16` | Cap on parallel upstream item fetches |
| `Cache:StoryIdsTtlSeconds` | `120` | TTL for the best-story id list |
| `Cache:StoryItemTtlSeconds` | `300` | TTL for individual items |
| `RateLimiting:PermitLimit` / `WindowSeconds` | `100` / `10` | Fixed window rate limit per client IP |
| `Authentication:ApiKey` | *(unset)* | Expected `X-Api-Key` value. Auth is off if unset |

## Authentication

There's an API key check on the `X-Api-Key` header, done as a proper
`AuthenticationHandler` with a constant-time comparison. It's config-gated: if
`Authentication:ApiKey` isn't set the handler treats everything as authenticated,
so you can run it locally without faffing with keys. Set it via user-secrets or
an env var and you get real 401s:

```bash
cd src/BestStories.Api
dotnet user-secrets set "Authentication:ApiKey" "my-secret-key"
# or env var: Authentication__ApiKey
curl -H "X-Api-Key: my-secret-key" "http://localhost:5009/api/v1/beststories?n=5"
```

The spec didn't ask for auth, so I made it opt-in rather than making the
reviewer dig out a key. In a real system this would be OIDC/JWT.

Swagger UI has an **Authorize** button wired up when running in Development.

## Architecture

Minimal API with a vertical slice for the one feature (`Features/BestStories`).
I kept it deliberately small: for a single endpoint, MediatR or a multi-project
Clean Architecture layout would just be ceremony. If this grew to multiple
features or teams I'd reach for a mediator pattern, and the slice boundary
(`endpoint -> service -> IHackerNewsClient`) makes that a refactor rather than
a rewrite.

Cross-cutting concerns on `IHackerNewsClient` are Scrutor decorators:

```
CachedHackerNewsClient        (outermost, HybridCache with per-call TTLs)
  -> InstrumentedHackerNewsClient  (metrics + logs, only sees cache misses)
    -> HackerNewsClient            (typed HttpClient + standard resilience handler)
```

Putting `Cached` outermost means the instrumentation only counts genuine
upstream calls. If you wanted to measure hits too, swap the two `Decorate` calls.
I'm a fan of decorators for this kind of thing, but I kept it honest: resilience
stays on the `HttpMessageHandler`, exceptions go through `IExceptionHandler` as
ProblemDetails, and validation lives in an endpoint filter. Each concern sits at
the layer that owns it.

The "don't overload Hacker News" requirement is handled in layers:

1. `HybridCache` gives TTL'd entries plus stampede protection out of the box,
   so concurrent misses on the same key collapse into one upstream call. If this
   ever went multi-instance you'd register an `IDistributedCache` (Redis) and
   get an L2 for free.
2. The id list comes back ranked, so only the first *n* ids get fetched, not
   all 200.
3. Item fetches fan out with `Parallel.ForEachAsync` capped at `MaxConcurrency`,
   so a cold cache at n=200 fires at most 16 concurrent upstream calls.
4. The outbound resilience handler bounds retries and timeouts, and the inbound
   rate limiter protects the API itself.

The service is pure orchestration returning `FluentResults.Result<T>`, with error
metadata mapped to RFC 7807 ProblemDetails.

A few perf notes in case anyone reads closely: JSON uses source generation
(`AppJsonSerializerContext`), `IHackerNewsClient` returns `ValueTask` so cache
hits don't allocate a `Task`, the `GetOrCreateAsync` calls use the state-based
overload with static lambdas to avoid closure allocations, and cache entry
options are hoisted to readonly fields rather than allocated per call.

## Assumptions

- `beststories.json` returns at most 200 ids (checked live), so `MaxStoryCount`
  caps `n` at the size of the data domain rather than an arbitrary number. That's
  also why I skipped pagination: `n` is already the page size and the whole set
  fits in one response.
- The id list is ranked best-first but item scores drift, so the response is
  re-sorted by `score` descending as the spec asks.
- `deleted`/`dead` items and non-`story` types get filtered out.
- If an individual item fetch fails after retries it gets skipped (best effort),
  but if no items come back at all the API returns 503 rather than an empty 200.
- Fewer than *n* retrievable stories means you get what there is.
- Cache TTLs trade freshness for upstream load; a couple of minutes of stale
  scores seemed like the right trade here.

## Given more time

- Redis L2 behind `IDistributedCache` for multi-instance (HybridCache picks it
  up automatically), plus a compose service for it
- `AddOutputCache` vary-by-`n` for identical queries
- OpenTelemetry export; the `Meter` instruments are already in place
- CI (GitHub Actions: build, test, format, container build)
- Serilog and correlation ids
- OIDC/JWT in place of the API key
- Latency and chaos coverage in the integration suite (WireMock `fixedDelay`)
