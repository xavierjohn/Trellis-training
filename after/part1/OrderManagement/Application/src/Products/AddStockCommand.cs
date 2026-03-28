namespace OrderManagement.Application.Products;

using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>
/// Adds stock to a product.
/// </summary>
public sealed record AddStockCommand(
    ProductId ProductId,
    StockQuantity Quantity) : ICommand<Result<Product>>, IAuthorize
{
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.ProductsManageStock];
}

/// <summary>
/// Handler for AddStockCommand.
/// </summary>
public sealed class AddStockCommandHandler : ICommandHandler<AddStockCommand, Result<Product>>
{
    private readonly IProductRepository _repository;

    public AddStockCommandHandler(IProductRepository repository) => _repository = repository;

    public async ValueTask<Result<Product>> Handle(AddStockCommand command, CancellationToken cancellationToken) =>
        await (await _repository.FindByIdAsync(command.ProductId, cancellationToken))
            .ToResult(Error.NotFound($"Product {command.ProductId} not found."))
            .Bind(product => product.AddStock(command.Quantity))
            .BindAsync(product => _repository.SaveAsync(product, cancellationToken).MapAsync(_ => product));
}
