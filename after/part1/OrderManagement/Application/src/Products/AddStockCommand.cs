namespace OrderManagement.Application.Products;

using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>Adds stock to a product.</summary>
public sealed record AddStockCommand(ProductId ProductId, StockQuantity Quantity) : ICommand<Result<Product>>, IAuthorize
{
    /// <inheritdoc />
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.ProductsManageStock];
}

/// <summary>Handler for <see cref="AddStockCommand"/>.</summary>
public sealed class AddStockCommandHandler : ICommandHandler<AddStockCommand, Result<Product>>
{
    private readonly IProductRepository _repository;

    /// <summary>Initializes a new instance of <see cref="AddStockCommandHandler"/>.</summary>
    public AddStockCommandHandler(IProductRepository repository) => _repository = repository;

    /// <inheritdoc />
    public async ValueTask<Result<Product>> Handle(AddStockCommand command, CancellationToken cancellationToken) =>
        await (await _repository.FindByIdAsync(command.ProductId, cancellationToken))
            .ToResult(Error.NotFound($"Product {command.ProductId} not found.", "productId"))
            .Bind(product => product.AddStock(command.Quantity))
            .BindAsync(product => _repository.SaveAsync(product, cancellationToken).MapAsync(_ => product));
}
