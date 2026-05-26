namespace Contracts;

public sealed record RouteLatencySummary(
    string UpstreamService,
    string RoutePattern,
    long RequestCount,
    double AverageLatencyMs,
    double P50LatencyMs,
    double P95LatencyMs,
    double P99LatencyMs);
