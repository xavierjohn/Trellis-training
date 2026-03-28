namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>Marks a shipped order as delivered.</summary>
public sealed record DeliverOrderCommand(OrderId OrderId) : ICommand<Result<Order>>, IAuthorize
{
    /// <inheritdoc />
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersDeliver];
}

/// <summary>Handler for <see cref="DeliverOrderCommand"/>.</summary>
public sealed class DeliverOrderCommandHandler : ICommandHandler<DeliverOrderCommand, Result<Order>>
{
    private readonly IOrderRepository _orderRepository;

    /// <summary>Initializes a new instance of <see cref="DeliverOrderCommandHandler"/>.</summary>
    public DeliverOrderCommandHandler(IOrderRepository orderRepository) => _orderRepository = orderRepository;

    /// <inheritdoc />
    public async ValueTask<Result<Order>> Handle(DeliverOrderCommand command, CancellationToken cancellationToken) =>
        await (await _orderRepository.FindByIdAsync(command.OrderId, cancellationToken))
            .ToResult(Error.NotFound($"Order {command.OrderId} not found.", "orderId"))
            .Bind(order => order.Deliver())
            .BindAsync(order => _orderRepository.SaveAsync(order, cancellationToken).MapAsync(_ => order));
}
