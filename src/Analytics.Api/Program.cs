using System.Data.Common;
using ClickHouse.Driver;
using Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddClickHouseDataSource("gatewayanalytics");

var app = builder.Build();

app.MapDefaultEndpoints();

await EnsureSchemaAsync(app.Services.GetRequiredService<IClickHouseClient>(), app.Lifetime.ApplicationStopping);

app.MapGet("/", () => Results.Ok(new { service = "analytics", status = "ok" }));

app.MapGet("/api/analytics/overview", async (int? minutes, IClickHouseClient client, CancellationToken cancellationToken) =>
{
    var windowMinutes = Math.Clamp(minutes ?? 60, 5, 24 * 60);

    var overviewTask = QueryOverviewAsync(client, windowMinutes, cancellationToken);
    var routeLatencyTask = QueryRouteLatencyAsync(client, windowMinutes, cancellationToken);
    var errorRatesTask = QueryErrorRatesAsync(client, windowMinutes, cancellationToken);
    var throughputTask = QueryThroughputAsync(client, windowMinutes, cancellationToken);
    var slowRequestsTask = QuerySlowRequestsAsync(client, windowMinutes, cancellationToken);

    await Task.WhenAll(overviewTask, routeLatencyTask, errorRatesTask, throughputTask, slowRequestsTask);

    return Results.Ok(new DashboardSnapshot(
        windowMinutes,
        await overviewTask,
        await routeLatencyTask,
        await errorRatesTask,
        await throughputTask,
        await slowRequestsTask));
});

app.Run();

static async Task EnsureSchemaAsync(IClickHouseClient client, CancellationToken cancellationToken)
{
    await client.ExecuteNonQueryAsync(ClickHouseSchema.CreateRequestLogsTable, cancellationToken: cancellationToken);
    await client.ExecuteNonQueryAsync(ClickHouseSchema.CreateRequestStatsMaterializedView, cancellationToken: cancellationToken);
}

static async Task<OverviewMetrics> QueryOverviewAsync(IClickHouseClient client, int minutes, CancellationToken cancellationToken)
{
    var sql =
        $$"""
        SELECT
            counts.total_requests AS total_requests,
            if(counts.total_requests = 0, 0, round(latency.avg_latency_ms, 2)) AS avg_latency_ms,
            if(counts.total_requests = 0, 0, round(latency.p95_latency_ms, 2)) AS p95_latency_ms,
            round(if(counts.total_requests = 0, 0, counts.error_count / counts.total_requests), 4) AS error_rate
        FROM (
            SELECT
                sum(request_count) AS total_requests,
                sumIf(request_count, status_code >= 500) AS error_count
            FROM (
                SELECT
                    status_code,
                    countMerge(request_count) AS request_count
                FROM request_stats_mv
                WHERE minute >= toStartOfMinute(now() - INTERVAL {{minutes}} MINUTE)
                GROUP BY status_code
            )
        ) AS counts
        CROSS JOIN (
            SELECT
                avgMerge(avg_duration) AS avg_latency_ms,
                quantilesTDigestMerge(0.5, 0.95, 0.99)(duration_quantiles)[2] AS p95_latency_ms
            FROM request_stats_mv
            WHERE minute >= toStartOfMinute(now() - INTERVAL {{minutes}} MINUTE)
        ) AS latency
        """;

    await using var reader = await client.ExecuteReaderAsync(sql, cancellationToken: cancellationToken);
    if (!await reader.ReadAsync(cancellationToken))
    {
        return new OverviewMetrics(0, 0, 0, 0);
    }

    return new OverviewMetrics(
        ReadInt64(reader, 0),
        ReadDouble(reader, 1),
        ReadDouble(reader, 2),
        ReadDouble(reader, 3));
}

static async Task<IReadOnlyList<RouteLatencySummary>> QueryRouteLatencyAsync(IClickHouseClient client, int minutes, CancellationToken cancellationToken)
{
    var sql =
        $$"""
        SELECT
            upstream_service,
            route_pattern,
            countMerge(request_count) AS request_count,
            round(avgMerge(avg_duration), 2) AS avg_latency_ms,
            round(quantilesTDigestMerge(0.5, 0.95, 0.99)(duration_quantiles)[1], 2) AS p50_latency_ms,
            round(quantilesTDigestMerge(0.5, 0.95, 0.99)(duration_quantiles)[2], 2) AS p95_latency_ms,
            round(quantilesTDigestMerge(0.5, 0.95, 0.99)(duration_quantiles)[3], 2) AS p99_latency_ms
        FROM request_stats_mv
        WHERE minute >= toStartOfMinute(now() - INTERVAL {{minutes}} MINUTE)
        GROUP BY upstream_service, route_pattern
        ORDER BY p95_latency_ms DESC
        """;

    var rows = new List<RouteLatencySummary>();
    await using var reader = await client.ExecuteReaderAsync(sql, cancellationToken: cancellationToken);

    while (await reader.ReadAsync(cancellationToken))
    {
        rows.Add(new RouteLatencySummary(
            reader.GetString(0),
            reader.GetString(1),
            ReadInt64(reader, 2),
            ReadDouble(reader, 3),
            ReadDouble(reader, 4),
            ReadDouble(reader, 5),
            ReadDouble(reader, 6)));
    }

    return rows;
}

static async Task<IReadOnlyList<ErrorRatePoint>> QueryErrorRatesAsync(IClickHouseClient client, int minutes, CancellationToken cancellationToken)
{
    var sql =
        $$"""
        SELECT
            minute,
            upstream_service,
            sum(request_count) AS total_requests,
            sumIf(request_count, status_code >= 500) AS error_count,
            round(if(total_requests = 0, 0, error_count / total_requests), 4) AS error_rate
        FROM (
            SELECT
                minute,
                upstream_service,
                status_code,
                countMerge(request_count) AS request_count
            FROM request_stats_mv
            WHERE minute >= toStartOfMinute(now() - INTERVAL {{minutes}} MINUTE)
            GROUP BY minute, upstream_service, status_code
        )
        GROUP BY minute, upstream_service
        ORDER BY minute ASC, upstream_service ASC
        """;

    var rows = new List<ErrorRatePoint>();
    await using var reader = await client.ExecuteReaderAsync(sql, cancellationToken: cancellationToken);

    while (await reader.ReadAsync(cancellationToken))
    {
        rows.Add(new ErrorRatePoint(
            reader.GetDateTime(0),
            reader.GetString(1),
            ReadInt64(reader, 2),
            ReadInt64(reader, 3),
            ReadDouble(reader, 4)));
    }

    return rows;
}

static async Task<IReadOnlyList<ThroughputPoint>> QueryThroughputAsync(IClickHouseClient client, int minutes, CancellationToken cancellationToken)
{
    var sql =
        $$"""
        SELECT
            minute,
            countMerge(request_count) AS total_requests
        FROM request_stats_mv
        WHERE minute >= toStartOfMinute(now() - INTERVAL {{minutes}} MINUTE)
        GROUP BY minute
        ORDER BY minute ASC
        """;

    var rows = new List<ThroughputPoint>();
    await using var reader = await client.ExecuteReaderAsync(sql, cancellationToken: cancellationToken);

    while (await reader.ReadAsync(cancellationToken))
    {
        rows.Add(new ThroughputPoint(
            reader.GetDateTime(0),
            ReadInt64(reader, 1)));
    }

    return rows;
}

static async Task<IReadOnlyList<SlowRequestItem>> QuerySlowRequestsAsync(IClickHouseClient client, int minutes, CancellationToken cancellationToken)
{
    var sql =
        $$"""
        SELECT
            trace_id,
            upstream_service,
            route_pattern,
            status_code,
            duration_ms,
            timestamp,
            error_message
        FROM request_logs
        WHERE timestamp >= now() - INTERVAL {{minutes}} MINUTE
        ORDER BY duration_ms DESC
        LIMIT 12
        """;

    var rows = new List<SlowRequestItem>();
    await using var reader = await client.ExecuteReaderAsync(sql, cancellationToken: cancellationToken);

    while (await reader.ReadAsync(cancellationToken))
    {
        rows.Add(new SlowRequestItem(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            Convert.ToUInt16(reader.GetValue(3)),
            ReadDouble(reader, 4),
            reader.GetDateTime(5),
            reader.IsDBNull(6) ? null : reader.GetString(6)));
    }

    return rows;
}

static long ReadInt64(DbDataReader reader, int ordinal)
{
    return Convert.ToInt64(reader.GetValue(ordinal));
}

static double ReadDouble(DbDataReader reader, int ordinal)
{
    return reader.IsDBNull(ordinal) ? 0 : Convert.ToDouble(reader.GetValue(ordinal));
}
