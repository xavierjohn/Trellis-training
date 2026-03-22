namespace OrderManagement.Application.Commands;

using Mediator;
using OrderManagement.Application.Abstractions;

public sealed class CancelOrderCommandHandler(
    IOrderRepository orderRepository,
    IProductRepository productRepository)
    : ICommandHandler<CancelOrderCommand, Result<Order>>
{
    public async ValueTask<Result<Order>> Handle(CancelOrderCommand command, CancellationToken cancellationToken)
    {
        var orderResult = await orderRepository.GetByIdAsync(command.OrderId, cancellationToken);
        if (!orderResult.TryGetValue(out var order))
            return orderResult.Error;

        List<Product>? products = null;
        if (order.Status.Is(OrderStatus.Submitted, OrderStatus.Approved))
        {
            var productIds = order.LineItems.Select(li => li.ProductId).ToList();
            var productsResult = await productRepository.GetByIdsAsync(productIds, cancellationToken);
            if (!productsResult.TryGetValue(out products))
                return productsResult.Error;
        }

        var cancelResult = order.Cancel(products);
        if (!cancelResult.TryGetValue(out var cancelledOrder))
            return cancelResult.Error;

        if (products is not null)
        {
            foreach (var product in products)
            {
                var saveProductResult = await productRepository.SaveAsync(product, cancellationToken);
                if (saveProductResult.TryGetError(out var saveProductError))
                    return saveProductError;
            }
        }

        var saveOrderResult = await orderRepository.SaveAsync(cancelledOrder, cancellationToken);
        if (saveOrderResult.TryGetError(out var saveOrderError))
            return saveOrderError;

        return cancelledOrder;
    }
}
