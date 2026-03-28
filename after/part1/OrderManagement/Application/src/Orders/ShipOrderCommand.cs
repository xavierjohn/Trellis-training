namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>Ships an approved order.</summary>
public sealed record ShipOrderCommand(OrderId OrderId) : ICommand<Result<Order>>, IAuthorize
{
    /// <inheritdoc />
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersShip];
}

/// <summary>Handler for <see cref="ShipOrderCommand"/>.</summary>
public sealed class ShipOrderCommandHandler : ICommandHandler<ShipOrderCommand, Result<Order>>
{
    private readonly IOrderRepository _orderRepository;

    /// <summary>Initializes a new instance of <see cref="ShipOrderCommandHandler"/>.</summary>
    public ShipOrderCommandHandler(IOrderRepository orderRepository) => _orderRepository = orderRepository;

    /// <inheritdoc />
    public async ValueTask<Result<Order>> Handle(ShipOrderCommand command, CancellationToken cancellationToken) =>
        await (await _orderRepository.FindByIdAsync(command.OrderId, cancellationToken))
            .ToResult(Error.NotFound($"Order {command.OrderId} not found.", "orderId"))
            .Bind(order => order.Ship())
            .BindAsync(order => _orderRepository.SaveAsync(order, cancellationToken).MapAsync(_ => order));
}
