namespace OrderManagement.Application.Products;

using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;

public sealed record AddStockCommand(ProductId ProductId, StockQuantity Quantity) : ICommand<Result<Product>>, IAuthorize
{
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.ProductsManageStock];
}

public sealed class AddStockCommandHandler : ICommandHandler<AddStockCommand, Result<Product>>
{
    private readonly IProductRepository _repository;

    public AddStockCommandHandler(IProductRepository repository) => _repository = repository;

    public async ValueTask<Result<Product>> Handle(AddStockCommand command, CancellationToken cancellationToken) =>
        await (await _repository.FindByIdAsync(command.ProductId, cancellationToken))
            .ToResult(Error.NotFound($"Product {command.ProductId.Value} not found."))
            .Bind(product => product.AddStock(command.Quantity))
            .CheckAsync(product => _repository.SaveAsync(product, cancellationToken));
}
