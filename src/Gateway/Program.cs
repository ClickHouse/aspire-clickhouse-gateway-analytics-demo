using System.Diagnostics;
using Gateway;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddClickHouseDataSource("gatewayanalytics");
builder.Services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddMeter(GatewayMetrics.MeterName));
builder.Services.AddSingleton<RequestLogQueue>();
builder.Services.AddHostedService<RequestLogPump>();

var routes = new[]
{
    new RouteConfig
    {
        RouteId = "products-list",
        ClusterId = "products-cluster",
        Match = new RouteMatch { Path = "/api/products" },
    },
    new RouteConfig
    {
        RouteId = "product-by-id",
        ClusterId = "products-cluster",
        Match = new RouteMatch { Path = "/api/products/{id:int}" },
    },
    new RouteConfig
    {
        RouteId = "orders-list",
        ClusterId = "orders-cluster",
        Match = new RouteMatch { Path = "/api/orders" },
    },
    new RouteConfig
    {
        RouteId = "order-by-id",
        ClusterId = "orders-cluster",
        Match = new RouteMatch { Path = "/api/orders/{id:int}" },
    },
    new RouteConfig
    {
        RouteId = "orders-checkout",
        ClusterId = "orders-cluster",
        Match = new RouteMatch { Path = "/api/orders/checkout" },
    },
};

var clusters = new[]
{
    new ClusterConfig
    {
        ClusterId = "products-cluster",
        Destinations = new Dictionary<string, DestinationConfig>
        {
            ["products"] = new() { Address = EnsureTrailingSlash(GetRequiredServiceUrl(builder.Configuration, "ProductsUrl")) },
        },
    },
    new ClusterConfig
    {
        ClusterId = "orders-cluster",
        Destinations = new Dictionary<string, DestinationConfig>
        {
            ["orders"] = new() { Address = EnsureTrailingSlash(GetRequiredServiceUrl(builder.Configuration, "OrdersUrl")) },
        },
    },
};

builder.Services.AddReverseProxy().LoadFromMemory(routes, clusters);

var app = builder.Build();

app.MapDefaultEndpoints();

app.Use(async (context, next) =>
{
    var startedAt = DateTime.UtcNow;
    var stopwatch = Stopwatch.StartNew();
    string? errorMessage = null;

    try
    {
        await next(context);
    }
    catch (Exception ex)
    {
        errorMessage = ex.Message;
        throw;
    }
    finally
    {
        stopwatch.Stop();

        if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            var routePattern = NormalizeRoute(context.Request.Path);
            var upstreamService = ResolveService(context.Request.Path);
            var statusClass = GetStatusClass(context.Response.StatusCode);
            var tags = new TagList
            {
                { "gateway.route", routePattern },
                { "gateway.service", upstreamService },
                { "gateway.status_class", statusClass },
            };

            GatewayMetrics.Requests.Add(1, tags);
            GatewayMetrics.DurationMs.Record(stopwatch.Elapsed.TotalMilliseconds, tags);

            if (context.Response.StatusCode >= 500 || errorMessage is not null)
            {
                GatewayMetrics.BackendFailures.Add(1, tags);
            }

            var logEntry = new RequestLogRow(
                Guid.NewGuid(),
                Activity.Current?.TraceId.ToString() ?? string.Empty,
                startedAt,
                context.Request.Method,
                routePattern,
                context.Request.Path.Value ?? string.Empty,
                upstreamService,
                (ushort)context.Response.StatusCode,
                stopwatch.Elapsed.TotalMilliseconds,
                (uint)Math.Clamp(context.Request.ContentLength ?? 0, 0, uint.MaxValue),
                (uint)Math.Clamp(context.Response.ContentLength ?? 0, 0, uint.MaxValue),
                errorMessage);

            if (!context.RequestServices.GetRequiredService<RequestLogQueue>().TryEnqueue(logEntry))
            {
                GatewayMetrics.LogDrops.Add(1, tags);
            }
        }
    }
});

app.MapGet("/", () => Results.Ok(new
{
    service = "gateway",
    focus = "proxy + clickhouse analytics",
    routes = routes.Select(route => route.Match.Path),
}));

app.MapReverseProxy();

app.Run();

static string GetRequiredServiceUrl(IConfiguration configuration, string key)
{
    return configuration[$"Services:{key}"]
        ?? throw new InvalidOperationException($"Missing configuration value 'Services:{key}'.");
}

static string EnsureTrailingSlash(string address)
{
    return address.EndsWith("/", StringComparison.Ordinal) ? address : $"{address}/";
}

static string ResolveService(PathString path)
{
    var value = path.Value ?? string.Empty;
    return value.StartsWith("/api/products", StringComparison.OrdinalIgnoreCase) ? "products" : "orders";
}

static string NormalizeRoute(PathString path)
{
    var value = path.Value ?? string.Empty;

    return value.ToLowerInvariant() switch
    {
        "/api/products" => "/api/products",
        "/api/orders" => "/api/orders",
        "/api/orders/checkout" => "/api/orders/checkout",
        _ when value.StartsWith("/api/products/", StringComparison.OrdinalIgnoreCase) => "/api/products/{id}",
        _ when value.StartsWith("/api/orders/", StringComparison.OrdinalIgnoreCase) => "/api/orders/{id}",
        _ => value,
    };
}

static string GetStatusClass(int statusCode)
{
    return statusCode switch
    {
        >= 500 => "5xx",
        >= 400 => "4xx",
        >= 300 => "3xx",
        >= 200 => "2xx",
        _ => "other",
    };
}

