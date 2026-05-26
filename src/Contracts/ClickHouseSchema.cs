namespace Contracts;

public static class ClickHouseSchema
{
    public const string CreateRequestLogsTable =
        """
        CREATE TABLE IF NOT EXISTS request_logs (
            request_id UUID,
            trace_id String,
            timestamp DateTime64(6, 'UTC'),
            method LowCardinality(String),
            route_pattern LowCardinality(String),
            path String,
            upstream_service LowCardinality(String),
            status_code UInt16,
            duration_ms Float64,
            request_size UInt32,
            response_size UInt32,
            error_message Nullable(String)
        )
        ENGINE = MergeTree()
        PARTITION BY toYYYYMMDD(timestamp)
        ORDER BY (upstream_service, route_pattern, timestamp)
        TTL timestamp + INTERVAL 30 DAY
        """;

    public const string CreateRequestStatsMaterializedView =
        """
        CREATE MATERIALIZED VIEW IF NOT EXISTS request_stats_mv
        ENGINE = AggregatingMergeTree()
        ORDER BY (upstream_service, route_pattern, status_code, minute)
        AS
        SELECT
            upstream_service,
            route_pattern,
            status_code,
            toStartOfMinute(timestamp) AS minute,
            countState() AS request_count,
            avgState(duration_ms) AS avg_duration,
            quantilesTDigestState(0.5, 0.95, 0.99)(duration_ms) AS duration_quantiles,
            maxState(duration_ms) AS max_duration
        FROM request_logs
        GROUP BY upstream_service, route_pattern, status_code, minute
        """;
}
