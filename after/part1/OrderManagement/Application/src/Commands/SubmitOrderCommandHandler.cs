namespace OrderManagement.Application.Commands;

using Mediator;
using OrderManagement.Application.Abstractions;

public sealed class SubmitOrderCommandHandler(
    IOrderRepository orderRepository,
    IProductRepository productRepository)
    : ICommandHandler<SubmitOrderCommand, Result<Order>>
{
    public async ValueTask<Result<Order>> Handle(SubmitOrderCommand command, CancellationToken cancellationToken)
    {
        var orderResult = await orderRepository.GetByIdAsync(command.OrderId, cancellationToken);
        if (!orderResult.TryGetValue(out var order))
            return orderResult.Error;

        var productIds = order.LineItems.Select(li => li.ProductId).ToList();
        var productsResult = await productRepository.GetByIdsAsync(productIds, cancellationToken);
        if (!productsResult.TryGetValue(out var products))
            return productsResult.Error;

        var submitResult = order.Submit(products);
        if (!submitResult.TryGetValue(out var submittedOrder))
            return submitResult.Error;

        foreach (var product in products)
        {
            var saveProductResult = await productRepository.SaveAsync(product, cancellationToken);
            if (saveProductResult.TryGetError(out var saveProductError))
                return saveProductError;
        }

        var saveOrderResult = await orderRepository.SaveAsync(submittedOrder, cancellationToken);
        if (saveOrderResult.TryGetError(out var saveOrderError))
            return saveOrderError;

        return submittedOrder;
    }
}
