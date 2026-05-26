namespace Contracts;

public sealed record DashboardSnapshot(
    int WindowMinutes,
    OverviewMetrics Overview,
    IReadOnlyList<RouteLatencySummary> RouteLatency,
    IReadOnlyList<ErrorRatePoint> ErrorRates,
    IReadOnlyList<ThroughputPoint> Throughput,
    IReadOnlyList<SlowRequestItem> SlowRequests);
