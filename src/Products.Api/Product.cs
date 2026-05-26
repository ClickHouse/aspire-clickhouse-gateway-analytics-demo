namespace Products.Api;

internal sealed record Product(int Id, string Name, string Description, decimal Price, string Category);
