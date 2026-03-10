namespace OrderManagement.Application.Products;

using Mediator;
using OrderManagement.Domain.Aggregates;
using OrderManagement.Domain.ValueObjects;
using Trellis.Authorization;
using Trellis.Mediator;

public sealed record AddStockCommand(
    ProductId ProductId,
    int Quantity) : ICommand<Result<Product>>, IAuthorize
{
    public IReadOnlyList<string> RequiredPermissions => [Domain.Permissions.ProductsManageStock];
}
