namespace OrderManagement.Application.Commands;

using Mediator;
using OrderManagement.Application.Abstractions;

public sealed class RemoveLineItemCommandHandler(IOrderRepository orderRepository)
    : ICommandHandler<RemoveLineItemCommand, Result<Order>>
{
    public async ValueTask<Result<Order>> Handle(RemoveLineItemCommand command, CancellationToken cancellationToken) =>
        await orderRepository.GetByIdAsync(command.OrderId, cancellationToken)
            .BindAsync(order => order.RemoveLineItem(command.LineItemId))
            .TapAsync(order => orderRepository.SaveAsync(order, cancellationToken));
}
