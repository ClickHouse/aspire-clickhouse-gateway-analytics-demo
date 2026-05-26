using ClickHouse.Driver;

namespace Gateway;

internal sealed record RequestLogRow(
    [property: ClickHouseColumn(Name = "request_id")] Guid RequestId,
    [property: ClickHouseColumn(Name = "trace_id")] string TraceId,
    [property: ClickHouseColumn(Name = "timestamp")] DateTime Timestamp,
    [property: ClickHouseColumn(Name = "method")] string Method,
    [property: ClickHouseColumn(Name = "route_pattern")] string RoutePattern,
    [property: ClickHouseColumn(Name = "path")] string Path,
    [property: ClickHouseColumn(Name = "upstream_service")] string UpstreamService,
    [property: ClickHouseColumn(Name = "status_code")] ushort StatusCode,
    [property: ClickHouseColumn(Name = "duration_ms")] double DurationMs,
    [property: ClickHouseColumn(Name = "request_size")] uint RequestSize,
    [property: ClickHouseColumn(Name = "response_size")] uint ResponseSize,
    [property: ClickHouseColumn(Name = "error_message")] string? ErrorMessage);
