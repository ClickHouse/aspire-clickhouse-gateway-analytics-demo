# Aspire Gateway Analytics

This repo is a small Aspire demo that puts a YARP gateway in front of two minimal APIs, writes every proxied request to ClickHouse, and shows the results in a Blazor dashboard.

The sample is built to demonstrate the use of ClickHouse with its Aspire integrations.

## Projects

- `src/AppHost`: Aspire orchestration, ClickHouse resource, and service wiring
- `src/Gateway`: YARP reverse proxy, gateway metrics, and request logging to ClickHouse
- `src/Products.Api`: fast stable backend
- `src/Orders.Api`: backend with one intentionally degraded endpoint
- `src/Analytics.Api`: reads ClickHouse and serves dashboard-friendly aggregates
- `src/Dashboard`: Blazor UI for request analytics
- `src/LoadGenerator`: continuously drives traffic through the gateway
- `src/Contracts`: shared dashboard DTOs
- `src/ServiceDefaults`: shared health checks and OpenTelemetry wiring

## Prerequisites

- `.NET 10 SDK`
- Docker

## Run

Use the AppHost HTTP launch profile:

```bash
dotnet run --project src/AppHost/AppHost.csproj --launch-profile http
```

That starts all the services. You can then open:

- Aspire dashboard on `http://localhost:15000`
- ClickHouse API gateway dashboard on `http://localhost:5100`

The AppHost console will print a login URL for the Aspire dashboard.

## What To Look At

1. Open the Aspire dashboard and check out the service map.
2. Open the metrics view and look for the gateway custom signals:
   - `gateway.requests`
   - `gateway.duration`
   - `gateway.backend_failures`
3. Open the traces view and inspect a slow `orders/checkout` request.
4. Open the Blazor analytics dashboard and compare that trace-first experience with the ClickHouse summaries:
   - total requests
   - p95 latency
   - error rate
   - per-route percentile leaderboard
   - slow requests with `trace_id`

## Notes

- ClickHouse connectivity uses `Aspire.ClickHouse.Driver`.
- The sample writes through the client API, not ADO command/connection usage.
- Direct inserts are used intentionally, with ClickHouse async insert settings enabled per insert statement to keep the code small.
- The schema is created automatically by the gateway and analytics API on startup.