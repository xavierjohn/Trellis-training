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

public sealed class CreateProductCommandHandler(IProductRepository repository)
    : ICommandHandler<CreateProductCommand, Result<Product>>
{
    public async ValueTask<Result<Product>> Handle(CreateProductCommand command, CancellationToken cancellationToken)
    {
        if (await repository.ExistsBySkuAsync(command.Sku, cancellationToken).ConfigureAwait(false))
            return Result.Fail<Product>(Error.Conflict.For<Product>(
                "product.duplicate-sku", id: command.Sku.Value,
                detail: $"A product with SKU '{command.Sku.Value}' already exists."));

        var product = new Product(command.ProductName, command.Sku, command.UnitPrice);
        repository.Add(product);
        return Result.Ok(product);
    }
}
