namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Application.Repositories;
using OrderManagement.Domain.Aggregates;

public sealed class SubmitOrderHandler(
    IOrderRepository orderRepository,
    IProductRepository productRepository)
    : ICommandHandler<SubmitOrderCommand, Result<Order>>
{
    public async ValueTask<Result<Order>> Handle(SubmitOrderCommand command, CancellationToken cancellationToken)
    {
        var orderResult = await orderRepository.GetByIdAsync(command.OrderId, cancellationToken);
        if (orderResult.TryGetError(out var loadError)) return loadError;
        orderResult.TryGetValue(out var order);

        var productsToSave = new List<Product>();

        Result<Trellis.Unit> ReserveStock(Domain.ValueObjects.ProductId productId, int quantity)
        {
            var productResult = productRepository.GetByIdAsync(productId, cancellationToken).GetAwaiter().GetResult();
            if (productResult.TryGetError(out var productError)) return productError;
            productResult.TryGetValue(out var product);

            var reserveResult = product.ReserveStock(quantity);
            if (reserveResult.TryGetError(out var reserveError)) return reserveError;
            reserveResult.TryGetValue(out var updated);

            productsToSave.Add(updated);
            return Result.Success();
        }

        return await order.Submit(ReserveStock)
            .BindAsync((Func<Order, Task<Result<Order>>>)(async o =>
            {
                foreach (var product in productsToSave)
                {
                    var saveResult = await productRepository.SaveAsync(product, cancellationToken);
                    if (saveResult.TryGetError(out var saveError)) return saveError;
                }

                return await orderRepository.SaveAsync(o, cancellationToken);
            }));
    }
}
