namespace OrderManagement.Application.Commands;

using Mediator;
using OrderManagement.Application.Abstractions;

public sealed class AddStockCommandHandler(IProductRepository productRepository)
    : ICommandHandler<AddStockCommand, Result<Product>>
{
    public async ValueTask<Result<Product>> Handle(AddStockCommand command, CancellationToken cancellationToken) =>
        await productRepository.GetByIdAsync(command.ProductId, cancellationToken)
            .BindAsync(product => product.AddStock(command.Quantity))
            .TapAsync(product => productRepository.SaveAsync(product, cancellationToken));
}
