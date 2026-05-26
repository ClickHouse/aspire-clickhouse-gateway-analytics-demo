using System.Diagnostics.Metrics;

namespace Gateway;

internal static class GatewayMetrics
{
    public const string MeterName = "Gateway.Telemetry";

    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> Requests =
        Meter.CreateCounter<long>("gateway.requests", unit: "{request}", description: "Number of proxied requests seen by the gateway.");

    public static readonly Histogram<double> DurationMs =
        Meter.CreateHistogram<double>("gateway.duration", unit: "ms", description: "End-to-end proxy request duration.");

    public static readonly Counter<long> BackendFailures =
        Meter.CreateCounter<long>("gateway.backend_failures", unit: "{request}", description: "Number of proxied requests that resulted in backend failures.");

    public static readonly Counter<long> LogDrops =
        Meter.CreateCounter<long>("gateway.log_drops", unit: "{record}", description: "Number of request log rows dropped because the ingestion queue was full.");
}
