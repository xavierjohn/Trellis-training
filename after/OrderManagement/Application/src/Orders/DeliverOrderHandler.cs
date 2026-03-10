namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Application.Repositories;
using OrderManagement.Domain.Aggregates;

public sealed class DeliverOrderHandler(IOrderRepository orderRepository)
    : ICommandHandler<DeliverOrderCommand, Result<Order>>
{
    public async ValueTask<Result<Order>> Handle(DeliverOrderCommand command, CancellationToken cancellationToken) =>
        await orderRepository.GetByIdAsync(command.OrderId, cancellationToken)
            .BindAsync(order => order.Deliver())
            .BindAsync((Func<Order, Task<Result<Order>>>)(async order =>
                await orderRepository.SaveAsync(order, cancellationToken)));
}
