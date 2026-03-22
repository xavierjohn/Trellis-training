namespace OrderManagement.Application.Commands;

using Mediator;
using Trellis.Authorization;
using Trellis.Mediator;
using Trellis.Primitives;

public sealed record CreateProductCommand(
    ProductName ProductName,
    Sku Sku,
    Money UnitPrice)
    : ICommand<Result<Product>>, IAuthorize
{
    public IReadOnlyList<string> RequiredPermissions => [Permissions.ProductsCreate];
}
