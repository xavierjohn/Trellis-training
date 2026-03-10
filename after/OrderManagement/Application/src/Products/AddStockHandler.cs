namespace OrderManagement.Application.Products;

using Mediator;
using OrderManagement.Application.Repositories;
using OrderManagement.Domain.Aggregates;

public sealed class AddStockHandler(IProductRepository productRepository)
    : ICommandHandler<AddStockCommand, Result<Product>>
{
    public async ValueTask<Result<Product>> Handle(AddStockCommand command, CancellationToken cancellationToken) =>
        await productRepository.GetByIdAsync(command.ProductId, cancellationToken)
            .BindAsync(product => product.AddStock(command.Quantity))
            .BindAsync((Func<Product, Task<Result<Product>>>)(async product =>
                await productRepository.SaveAsync(product, cancellationToken)));
}
