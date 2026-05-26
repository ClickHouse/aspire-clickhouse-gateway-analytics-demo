using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var clickhouse = builder.AddClickHouse("clickhouse")
    .WithDataVolume();

var analyticsDb = clickhouse.AddDatabase("gatewayanalytics");

var products = builder.AddProject<Projects.Products_Api>("products");
var orders = builder.AddProject<Projects.Orders_Api>("orders");

var analytics = builder.AddProject<Projects.Analytics_Api>("analytics")
    .WithReference(analyticsDb)
    .WaitFor(analyticsDb);

var gateway = builder.AddProject<Projects.Gateway>("gateway")
    .WithExternalHttpEndpoints()
    .WithReference(analyticsDb)
    .WithEnvironment("Services__ProductsUrl", products.GetEndpoint("http"))
    .WithEnvironment("Services__OrdersUrl", orders.GetEndpoint("http"))
    .WaitFor(analyticsDb)
    .WaitFor(products)
    .WaitFor(orders);

var dashboard = builder.AddProject<Projects.Dashboard>("dashboard")
    .WithHttpEndpoint(port: 5100, name: "http")
    .WithExternalHttpEndpoints()
    .WithEnvironment("Analytics__BaseUrl", analytics.GetEndpoint("http"))
    .WaitFor(analytics);

builder.AddProject<Projects.LoadGenerator>("traffic")
    .WithEnvironment("Gateway__BaseUrl", gateway.GetEndpoint("http"))
    .WaitFor(gateway);

builder.Build().Run();
