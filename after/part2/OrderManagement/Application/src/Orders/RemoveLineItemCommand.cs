namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>
/// Removes a line item from a draft order.
/// </summary>
public sealed record RemoveLineItemCommand(
    OrderId OrderId,
    LineItemId LineItemId) : ICommand<Result<Order>>, IAuthorize
{
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersCreate];
}

/// <summary>
/// Handler for RemoveLineItemCommand.
/// </summary>
public sealed class RemoveLineItemCommandHandler : ICommandHandler<RemoveLineItemCommand, Result<Order>>
{
    private readonly IOrderRepository _orderRepository;

    public RemoveLineItemCommandHandler(IOrderRepository orderRepository) => _orderRepository = orderRepository;

    public async ValueTask<Result<Order>> Handle(RemoveLineItemCommand command, CancellationToken cancellationToken) =>
        await (await _orderRepository.FindByIdAsync(command.OrderId, cancellationToken))
            .ToResult(Error.NotFound($"Order {command.OrderId} not found."))
            .Bind(order => order.RemoveLineItem(command.LineItemId))
            .BindAsync(order => _orderRepository.SaveAsync(order, cancellationToken).MapAsync(_ => order));
}
