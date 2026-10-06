using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace BestStories.Api.Common.Observability;

/// <summary>
/// Metrics for genuine upstream Hacker News traffic. Registered as a singleton;
/// exposed for future OpenTelemetry export.
/// </summary>
public sealed class HackerNewsMetrics : IDisposable
{
    private readonly Meter _meter = new("BestStories.Api.HackerNews", "1.0.0");
    private readonly Counter<long> _calls;
    private readonly Histogram<double> _duration;

    public HackerNewsMetrics()
    {
        _calls = _meter.CreateCounter<long>(
            "hn.upstream.calls",
            description: "Number of outbound calls to the Hacker News API");
        _duration = _meter.CreateHistogram<double>(
            "hn.upstream.duration",
            unit: "ms",
            description: "Duration of outbound calls to the Hacker News API");
    }

    public void RecordCall(string operation, string outcome, double elapsedMilliseconds)
    {
        var tags = new TagList
        {
            { "operation", operation },
            { "outcome", outcome }
        };
        _calls.Add(1, tags);
        _duration.Record(elapsedMilliseconds, tags);
    }

    public void Dispose() => _meter.Dispose();
}
