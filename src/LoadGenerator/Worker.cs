namespace LoadGenerator;

public sealed class Worker(IHttpClientFactory httpClientFactory, ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(400));

        logger.LogInformation("Load generator started.");

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await DriveTrafficAsync(httpClientFactory.CreateClient("gateway"), stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Load generator request failed.");
            }
        }
    }

    private static async Task DriveTrafficAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var roll = Random.Shared.NextDouble();

        if (roll < 0.40)
        {
            using var response = await client.GetAsync("/api/products", cancellationToken);
            return;
        }

        if (roll < 0.62)
        {
            var id = Random.Shared.Next(1, 5);
            using var response = await client.GetAsync($"/api/products/{id}", cancellationToken);
            return;
        }

        if (roll < 0.78)
        {
            using var response = await client.GetAsync("/api/orders", cancellationToken);
            return;
        }

        if (roll < 0.88)
        {
            var id = Random.Shared.Next(101, 104);
            using var response = await client.GetAsync($"/api/orders/{id}", cancellationToken);
            return;
        }

        using var checkoutResponse = await client.GetAsync("/api/orders/checkout", cancellationToken);
    }
}
