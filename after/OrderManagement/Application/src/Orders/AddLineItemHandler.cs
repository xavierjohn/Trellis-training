namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Application.Repositories;
using OrderManagement.Domain.Aggregates;

public sealed class AddLineItemHandler(
    IOrderRepository orderRepository,
    IProductRepository productRepository)
    : ICommandHandler<AddLineItemCommand, Result<Order>>
{
    public async ValueTask<Result<Order>> Handle(AddLineItemCommand command, CancellationToken cancellationToken) =>
        await Result.ParallelAsync(
                () => orderRepository.GetByIdAsync(command.OrderId, cancellationToken),
                () => productRepository.GetByIdAsync(command.ProductId, cancellationToken))
            .WhenAllAsync()
            .BindAsync((Order order, Product product) =>
                LineItem.TryCreate(product.Id, product.ProductName, command.Quantity, product.UnitPrice)
                    .Bind(order.AddLineItem))
            .BindAsync((Func<Order, Task<Result<Order>>>)(async order =>
                await orderRepository.SaveAsync(order, cancellationToken)));
}
