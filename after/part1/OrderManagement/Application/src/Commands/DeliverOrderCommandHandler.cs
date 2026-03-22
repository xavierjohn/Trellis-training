namespace OrderManagement.Application.Commands;

using Mediator;
using OrderManagement.Application.Abstractions;

public sealed class DeliverOrderCommandHandler(IOrderRepository orderRepository)
    : ICommandHandler<DeliverOrderCommand, Result<Order>>
{
    public async ValueTask<Result<Order>> Handle(DeliverOrderCommand command, CancellationToken cancellationToken) =>
        await orderRepository.GetByIdAsync(command.OrderId, cancellationToken)
            .BindAsync(order => order.Deliver())
            .TapAsync(order => orderRepository.SaveAsync(order, cancellationToken));
}
