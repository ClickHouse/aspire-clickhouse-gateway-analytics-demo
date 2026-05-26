namespace Contracts;

public sealed record OverviewMetrics(
    long TotalRequests,
    double AverageLatencyMs,
    double P95LatencyMs,
    double ErrorRate);
