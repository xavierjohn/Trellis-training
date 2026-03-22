namespace OrderManagement.Application.Commands;

using Mediator;
using OrderManagement.Application.Abstractions;

public sealed class AddLineItemCommandHandler(
    IOrderRepository orderRepository,
    IProductRepository productRepository)
    : ICommandHandler<AddLineItemCommand, Result<Order>>
{
    public async ValueTask<Result<Order>> Handle(AddLineItemCommand command, CancellationToken cancellationToken)
    {
        var orderResult = await orderRepository.GetByIdAsync(command.OrderId, cancellationToken);
        if (!orderResult.TryGetValue(out var order))
            return orderResult.Error;

        var productResult = await productRepository.GetByIdAsync(command.ProductId, cancellationToken);
        if (!productResult.TryGetValue(out var product))
            return productResult.Error;

        return await order.AddLineItem(command.ProductId, product.ProductName.Value, command.Quantity, product.UnitPrice)
            .TapAsync(updated => orderRepository.SaveAsync(updated, cancellationToken));
    }
}
