namespace OrderManagement.Application.Commands;

using Mediator;
using Trellis.Authorization;
using Trellis.Mediator;

public sealed record AddStockCommand(ProductId ProductId, int Quantity)
    : ICommand<Result<Product>>, IAuthorize, IValidate
{
    public IReadOnlyList<string> RequiredPermissions => [Permissions.ProductsManageStock];

    public IResult Validate() =>
        Quantity > 0
            ? Result.Success()
            : Result.Failure(Error.Validation("Quantity must be positive", "quantity"));
}
