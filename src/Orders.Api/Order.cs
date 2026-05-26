namespace Orders.Api;

internal sealed record Order(int Id, string Status, decimal Total, int ItemCount);
