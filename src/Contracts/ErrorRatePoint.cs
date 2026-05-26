namespace Contracts;

public sealed record ErrorRatePoint(
    DateTime Minute,
    string UpstreamService,
    long TotalRequests,
    long ErrorCount,
    double ErrorRate);
