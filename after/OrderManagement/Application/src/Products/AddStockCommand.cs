namespace OrderManagement.Application.Products;

using FluentValidation;
using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>Adds stock to an existing product.</summary>
public sealed record AddStockCommand(ProductId ProductId, StockQuantity Quantity)
    : ICommand<Result<Product>>, IAuthorize
{
    /// <inheritdoc />
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.ProductsManageStock];
}

public sealed class AddStockCommandValidator : AbstractValidator<AddStockCommand>
{
    public AddStockCommandValidator()
    {
        RuleFor(c => c.ProductId).NotNull();
        RuleFor(c => c.Quantity)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(q => q.Value > 0).WithMessage("Quantity must be positive.");
    }
}

public sealed class AddStockCommandHandler(IProductRepository repository)
    : ICommandHandler<AddStockCommand, Result<Product>>
{
    public async ValueTask<Result<Product>> Handle(AddStockCommand command, CancellationToken cancellationToken) =>
        await repository.FindByIdAsync(command.ProductId, cancellationToken)
            .ToResultAsync(() => Error.NotFound.For<Product>(
                id: command.ProductId, detail: $"Product {command.ProductId} not found."))
            .CheckAsync(product => product.AddStock(command.Quantity.Value))
            .ConfigureAwait(false);
}
