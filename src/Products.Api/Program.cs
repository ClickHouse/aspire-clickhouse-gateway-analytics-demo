using Microsoft.Extensions.Hosting;
using Products.Api;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var app = builder.Build();

app.MapDefaultEndpoints();

var products = new[]
{
    new Product(1, "Aspire Starter Pack", "Starter kits for local demos.", 49.00m, "Starter Kits"),
    new Product(2, "YARP Stress Ball", "A gateway-themed desk toy that does not route packets.", 12.50m, "Accessories"),
    new Product(3, "ClickHouse Espresso Beans", "Small-batch beans for long benchmarking sessions.", 18.95m, "Coffee"),
    new Product(4, "Observability Hoodie", "Warm enough for late-night trace spelunking.", 64.00m, "Apparel"),
};

app.MapGet("/", () => Results.Ok(new { service = "products", status = "ok" }));

app.MapGet("/api/products", async () =>
{
    await SimulateBackendLatency(baseMin: 8, baseMax: 45);
    return Results.Ok(products);
});

app.MapGet("/api/products/{id:int}", async (int id) =>
{
    await SimulateBackendLatency(baseMin: 15, baseMax: 70);
    var product = products.FirstOrDefault(item => item.Id == id);
    return product is null ? Results.NotFound() : Results.Ok(product);
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
