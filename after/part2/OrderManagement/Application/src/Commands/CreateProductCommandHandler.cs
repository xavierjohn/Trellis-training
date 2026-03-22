namespace OrderManagement.Application.Commands;

using Mediator;
using OrderManagement.Application.Abstractions;

public sealed class CreateProductCommandHandler(IProductRepository productRepository)
    : ICommandHandler<CreateProductCommand, Result<Product>>
{
    public async ValueTask<Result<Product>> Handle(CreateProductCommand command, CancellationToken cancellationToken) =>
        await Product.TryCreate(command.ProductName, command.Sku, command.UnitPrice)
            .TapAsync(product => productRepository.SaveAsync(product, cancellationToken));
}
