namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Application.Repositories;
using OrderManagement.Domain.Aggregates;
using OrderManagement.Domain.ValueObjects;

public sealed class CancelOrderHandler(
    IOrderRepository orderRepository,
    IProductRepository productRepository)
    : ICommandHandler<CancelOrderCommand, Result<Order>>
{
    public async ValueTask<Result<Order>> Handle(CancelOrderCommand command, CancellationToken cancellationToken) =>
        await orderRepository.GetByIdAsync(command.OrderId, cancellationToken)
            .BindAsync(order =>
            {
                void ReleaseStock(ProductId productId, int quantity)
                {
                    var productResult = productRepository.GetByIdAsync(productId, cancellationToken).GetAwaiter().GetResult();
                    if (productResult.TryGetValue(out var product))
                    {
                        var releaseResult = product.ReleaseStock(quantity);
                        if (releaseResult.TryGetValue(out var updated))
                        {
                            _ = productRepository.SaveAsync(updated, cancellationToken).GetAwaiter().GetResult();
                        }
                    }
                }

                return order.Cancel(ReleaseStock);
            })
            .BindAsync((Func<Order, Task<Result<Order>>>)(async order =>
                await orderRepository.SaveAsync(order, cancellationToken)));
}
