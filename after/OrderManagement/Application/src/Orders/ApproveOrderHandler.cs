namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Application.Repositories;
using OrderManagement.Domain.Aggregates;

public sealed class ApproveOrderHandler(IOrderRepository orderRepository)
    : ICommandHandler<ApproveOrderCommand, Result<Order>>
{
    public async ValueTask<Result<Order>> Handle(ApproveOrderCommand command, CancellationToken cancellationToken) =>
        await orderRepository.GetByIdAsync(command.OrderId, cancellationToken)
            .BindAsync(order => order.Approve())
            .BindAsync((Func<Order, Task<Result<Order>>>)(async order =>
                await orderRepository.SaveAsync(order, cancellationToken)));
}
