namespace OrderManagement.Application.Products;

using FluentValidation;
using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>Creates a new product.</summary>
public sealed record CreateProductCommand(
    ProductName ProductName,
    Sku Sku,
    UnitPrice UnitPrice) : ICommand<Result<Product>>, IAuthorize
{
    /// <inheritdoc />
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.ProductsCreate];
}

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(c => c.ProductName).NotNull();
        RuleFor(c => c.Sku).NotNull();
        RuleFor(c => c.UnitPrice).NotNull();
    }
}

public sealed class CreateProductCommandHandler : ICommandHandler<CreateProductCommand, Result<Product>>
{
    private readonly IProductRepository _repository;

    public CreateProductCommandHandler(IProductRepository repository) => _repository = repository;

    public async ValueTask<Result<Product>> Handle(CreateProductCommand command, CancellationToken cancellationToken)
    {
        if (await _repository.ExistsBySkuAsync(command.Sku, cancellationToken))
            return Result.Fail<Product>(new Error.Conflict(
                ResourceRef.For<Product>(command.Sku.Value),
                "product.duplicate-sku")
            { Detail = $"A product with SKU '{command.Sku.Value}' already exists." });

        var product = new Product(command.ProductName, command.Sku, command.UnitPrice);
        _repository.Add(product);
        return Result.Ok(product);
    }
}
