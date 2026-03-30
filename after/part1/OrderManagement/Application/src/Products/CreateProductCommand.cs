namespace OrderManagement.Application.Products;

using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;
using Trellis.Primitives;

public sealed record CreateProductCommand(
    ProductName ProductName,
    Sku Sku,
    Money UnitPrice) : ICommand<Result<Product>>, IAuthorize
{
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.ProductsCreate];
}

public sealed class CreateProductCommandHandler : ICommandHandler<CreateProductCommand, Result<Product>>
{
    private readonly IProductRepository _repository;

    public CreateProductCommandHandler(IProductRepository repository) => _repository = repository;

    public async ValueTask<Result<Product>> Handle(CreateProductCommand command, CancellationToken cancellationToken) =>
        await Product.TryCreate(command.ProductName, command.Sku, command.UnitPrice)
            .CheckAsync(product => _repository.SaveAsync(product, cancellationToken));
}
