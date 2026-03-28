namespace OrderManagement.Application.Products;

using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;
using Trellis.Primitives;

/// <summary>Creates a new product.</summary>
public sealed record CreateProductCommand(
    ProductName ProductName,
    Sku Sku,
    Money UnitPrice) : ICommand<Result<Product>>, IAuthorize
{
    /// <inheritdoc />
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.ProductsCreate];
}

/// <summary>Handler for <see cref="CreateProductCommand"/>.</summary>
public sealed class CreateProductCommandHandler : ICommandHandler<CreateProductCommand, Result<Product>>
{
    private readonly IProductRepository _repository;

    /// <summary>Initializes a new instance of <see cref="CreateProductCommandHandler"/>.</summary>
    public CreateProductCommandHandler(IProductRepository repository) => _repository = repository;

    /// <inheritdoc />
    public async ValueTask<Result<Product>> Handle(CreateProductCommand command, CancellationToken cancellationToken) =>
        await Product.TryCreate(command.ProductName, command.Sku, command.UnitPrice)
            .BindAsync(product => _repository.SaveAsync(product, cancellationToken).MapAsync(_ => product));
}
