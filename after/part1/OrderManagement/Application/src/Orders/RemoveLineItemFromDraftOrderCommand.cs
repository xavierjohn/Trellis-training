namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>Removes a line item from a draft order.</summary>
public sealed record RemoveLineItemFromDraftOrderCommand(
    OrderId OrderId,
    LineItemId LineItemId) : ICommand<Result<Order>>, IAuthorize
{
    /// <inheritdoc />
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersCreate];
}

/// <summary>Handler for <see cref="RemoveLineItemFromDraftOrderCommand"/>.</summary>
public sealed class RemoveLineItemFromDraftOrderCommandHandler
    : ICommandHandler<RemoveLineItemFromDraftOrderCommand, Result<Order>>
{
    private readonly IOrderRepository _orderRepository;

    /// <summary>Initializes a new instance of <see cref="RemoveLineItemFromDraftOrderCommandHandler"/>.</summary>
    public RemoveLineItemFromDraftOrderCommandHandler(IOrderRepository orderRepository) =>
        _orderRepository = orderRepository;

    /// <inheritdoc />
    public async ValueTask<Result<Order>> Handle(
        RemoveLineItemFromDraftOrderCommand command, CancellationToken cancellationToken) =>
        await (await _orderRepository.FindByIdAsync(command.OrderId, cancellationToken))
            .ToResult(Error.NotFound($"Order {command.OrderId} not found.", "orderId"))
            .Bind(order => order.RemoveLineItem(command.LineItemId))
            .BindAsync(order => _orderRepository.SaveAsync(order, cancellationToken).MapAsync(_ => order));
}
