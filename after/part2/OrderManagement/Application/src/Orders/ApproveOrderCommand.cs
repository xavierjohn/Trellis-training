namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>
/// Approves an order (Submitted → Approved).
/// </summary>
public sealed record ApproveOrderCommand(OrderId OrderId) : ICommand<Result<Order>>, IAuthorize
{
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersApprove];
}

/// <summary>
/// Handler for ApproveOrderCommand.
/// </summary>
public sealed class ApproveOrderCommandHandler : ICommandHandler<ApproveOrderCommand, Result<Order>>
{
    private readonly IOrderRepository _orderRepository;

    public ApproveOrderCommandHandler(IOrderRepository orderRepository) => _orderRepository = orderRepository;

    public async ValueTask<Result<Order>> Handle(ApproveOrderCommand command, CancellationToken cancellationToken) =>
        await (await _orderRepository.FindByIdAsync(command.OrderId, cancellationToken))
            .ToResult(Error.NotFound($"Order {command.OrderId} not found."))
            .Bind(order => order.Approve())
            .BindAsync(order => _orderRepository.SaveAsync(order, cancellationToken).MapAsync(_ => order));
}
