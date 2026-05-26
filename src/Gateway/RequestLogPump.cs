using ClickHouse.Driver;
using Contracts;

namespace Gateway;

internal sealed class RequestLogPump(
    IClickHouseClient client,
    RequestLogQueue queue,
    ILogger<RequestLogPump> logger) : BackgroundService
{
    private const int MaxBatchSize = 500;

    private static readonly InsertOptions InsertOptions = new()
    {
        CustomSettings = new Dictionary<string, object>
        {
            ["async_insert"] = 1,
            ["wait_for_async_insert"] = 1,
        },
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await client.ExecuteNonQueryAsync(ClickHouseSchema.CreateRequestLogsTable, cancellationToken: stoppingToken);
        await client.ExecuteNonQueryAsync(ClickHouseSchema.CreateRequestStatsMaterializedView, cancellationToken: stoppingToken);
        client.RegisterBinaryInsertType<RequestLogRow>();

        var batch = new List<RequestLogRow>(MaxBatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Block until at least one row is available — keeps the pump asleep when idle.
                batch.Add(await queue.Reader.ReadAsync(stoppingToken));
            }
            catch (OperationCanceledException)
            {
                break;
            }

            // Greedily drain whatever else is queued right now, up to the batch cap.
            while (batch.Count < MaxBatchSize && queue.Reader.TryRead(out var more))
            {
                batch.Add(more);
            }

            try
            {
                await client.InsertBinaryAsync("request_logs", batch, InsertOptions, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to write {Count} request log rows to ClickHouse.", batch.Count);
            }
            finally
            {
                batch.Clear();
            }
        }
    }
}
