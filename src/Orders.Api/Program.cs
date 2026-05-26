using Microsoft.Extensions.Hosting;
using Orders.Api;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var app = builder.Build();

app.MapDefaultEndpoints();

var orders = new[]
{
    new Order(101, "processing", 149.00m, 2),
    new Order(102, "shipped", 64.00m, 1),
    new Order(103, "pending", 18.95m, 3),
};

app.MapGet("/", () => Results.Ok(new { service = "orders", status = "ok" }));

app.MapGet("/api/orders", async () =>
{
    await SimulateBackendLatency(baseMin: 8, baseMax: 45);
    return Results.Ok(orders);
});

app.MapGet("/api/orders/{id:int}", async (int id) =>
{
    await SimulateBackendLatency(baseMin: 15, baseMax: 70);
    var order = orders.FirstOrDefault(item => item.Id == id);
    return order is null ? Results.NotFound() : Results.Ok(order);
});

app.MapGet("/api/orders/checkout", async (ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
{
    var logger = loggerFactory.CreateLogger("Orders.Checkout");

    var roll = Random.Shared.NextDouble();
    var delay = roll < 0.15 ? Random.Shared.Next(1600, 2400) : Random.Shared.Next(80, 260);
    await Task.Delay(delay, cancellationToken);

    if (roll < 0.10)
    {
        logger.LogWarning("Checkout downstream dependency timed out after {Delay}ms.", delay);
        return Results.Problem(
            detail: "The payment provider timed out while reserving inventory.",
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Checkout temporarily degraded");
    }

    return Results.Ok(new
    {
        confirmationId = $"CHK-{Random.Shared.Next(10000, 99999)}",
        simulatedLatencyMs = delay,
        status = delay > 1000 ? "completed-after-retry" : "completed",
    });
});

app.Run();

static async Task SimulateBackendLatency(int baseMin, int baseMax)
{
    var roll = Random.Shared.NextDouble();
    var delay = roll switch
    {
        < 0.70 => Random.Shared.Next(baseMin, baseMax),
        < 0.95 => Random.Shared.Next(baseMax, baseMax * 3),
        < 0.99 => Random.Shared.Next(baseMax * 3, baseMax * 6),
        _ => Random.Shared.Next(baseMax * 6, baseMax * 12),
    };
    await Task.Delay(delay);
}
