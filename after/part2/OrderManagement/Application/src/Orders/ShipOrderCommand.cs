namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>
/// Ships an order (Approved → Shipped).
/// </summary>
public sealed record ShipOrderCommand(OrderId OrderId) : ICommand<Result<Order>>, IAuthorize
{
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersShip];
}

/// <summary>
/// Handler for ShipOrderCommand.
/// </summary>
public sealed class ShipOrderCommandHandler : ICommandHandler<ShipOrderCommand, Result<Order>>
{
    private readonly IOrderRepository _orderRepository;

    public ShipOrderCommandHandler(IOrderRepository orderRepository) => _orderRepository = orderRepository;

    public async ValueTask<Result<Order>> Handle(ShipOrderCommand command, CancellationToken cancellationToken) =>
        await (await _orderRepository.FindByIdAsync(command.OrderId, cancellationToken))
            .ToResult(Error.NotFound($"Order {command.OrderId} not found."))
            .Bind(order => order.Ship(DateTime.UtcNow))
            .BindAsync(order => _orderRepository.SaveAsync(order, cancellationToken).MapAsync(_ => order));
}
