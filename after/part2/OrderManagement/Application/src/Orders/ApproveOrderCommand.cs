namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>Approves a submitted order.</summary>
public sealed record ApproveOrderCommand(OrderId OrderId) : ICommand<Result<Order>>, IAuthorize
{
    /// <inheritdoc />
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersApprove];
}

/// <summary>Handler for <see cref="ApproveOrderCommand"/>.</summary>
public sealed class ApproveOrderCommandHandler : ICommandHandler<ApproveOrderCommand, Result<Order>>
{
    private readonly IOrderRepository _orderRepository;

    /// <summary>Initializes a new instance of <see cref="ApproveOrderCommandHandler"/>.</summary>
    public ApproveOrderCommandHandler(IOrderRepository orderRepository) => _orderRepository = orderRepository;

    /// <inheritdoc />
    public async ValueTask<Result<Order>> Handle(ApproveOrderCommand command, CancellationToken cancellationToken) =>
        await (await _orderRepository.FindByIdAsync(command.OrderId, cancellationToken))
            .ToResult(Error.NotFound($"Order {command.OrderId} not found.", "orderId"))
            .Bind(order => order.Approve())
            .BindAsync(order => _orderRepository.SaveAsync(order, cancellationToken).MapAsync(_ => order));
}
