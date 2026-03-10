namespace OrderManagement.Application.Products;

using Mediator;
using OrderManagement.Domain.Aggregates;
using OrderManagement.Domain.ValueObjects;
using Trellis.Authorization;
using Trellis.Mediator;
using Trellis.Primitives;

public sealed record CreateProductCommand(
    ProductName ProductName,
    Sku Sku,
    Money UnitPrice) : ICommand<Result<Product>>, IAuthorize
{
    public IReadOnlyList<string> RequiredPermissions => [Domain.Permissions.ProductsCreate];
}
