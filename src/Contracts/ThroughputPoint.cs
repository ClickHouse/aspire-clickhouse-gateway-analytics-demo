namespace Contracts;

public sealed record ThroughputPoint(
    DateTime Minute,
    long TotalRequests);
