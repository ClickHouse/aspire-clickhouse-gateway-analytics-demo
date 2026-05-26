namespace Contracts;

public sealed record SlowRequestItem(
    string TraceId,
    string UpstreamService,
    string RoutePattern,
    ushort StatusCode,
    double DurationMs,
    DateTime Timestamp,
    string? ErrorMessage);
