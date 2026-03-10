namespace OrderManagement.Application.Products;

using Mediator;
using OrderManagement.Application.Repositories;
using OrderManagement.Domain.Aggregates;

public sealed class CreateProductHandler(IProductRepository productRepository)
    : ICommandHandler<CreateProductCommand, Result<Product>>
{
    public async ValueTask<Result<Product>> Handle(CreateProductCommand command, CancellationToken cancellationToken) =>
        await productRepository.ExistsBySkuAsync(command.Sku, cancellationToken)
            .BindAsync(exists => exists
                ? Result.Failure<Trellis.Unit>(Error.Conflict($"A product with SKU '{command.Sku}' already exists."))
                : Result.Success())
            .BindAsync(_ => Product.TryCreate(command.ProductName, command.Sku, command.UnitPrice))
            .BindAsync((Func<Product, Task<Result<Product>>>)(async product =>
                await productRepository.SaveAsync(product, cancellationToken)));
}
