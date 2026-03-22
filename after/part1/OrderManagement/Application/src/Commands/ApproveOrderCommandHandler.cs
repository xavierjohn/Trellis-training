namespace OrderManagement.Application.Commands;

using Mediator;
using OrderManagement.Application.Abstractions;

public sealed class ApproveOrderCommandHandler(IOrderRepository orderRepository)
    : ICommandHandler<ApproveOrderCommand, Result<Order>>
{
    public async ValueTask<Result<Order>> Handle(ApproveOrderCommand command, CancellationToken cancellationToken) =>
        await orderRepository.GetByIdAsync(command.OrderId, cancellationToken)
            .BindAsync(order => order.Approve())
            .TapAsync(order => orderRepository.SaveAsync(order, cancellationToken));
}
