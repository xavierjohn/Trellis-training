namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Application.Products;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>
/// Cancels an order. Includes ownership check.
/// </summary>
public sealed record CancelOrderCommand(OrderId OrderId) : ICommand<Result<Order>>, IAuthorize, IAuthorizeResource<Order>
{
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersCancel];

    public IResult Authorize(Actor actor, Order resource) =>
        Result.Ensure(
            actor.IsOwner(resource.CreatedByActorId) || actor.HasPermission(Permissions.OrdersReadAll),
            Error.Forbidden("Only the order creator or an admin can cancel this order."));
}

/// <summary>
/// Handler for CancelOrderCommand.
/// </summary>
public sealed class CancelOrderCommandHandler : ICommandHandler<CancelOrderCommand, Result<Order>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;

    public CancelOrderCommandHandler(IOrderRepository orderRepository, IProductRepository productRepository)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
    }

    public async ValueTask<Result<Order>> Handle(CancelOrderCommand command, CancellationToken cancellationToken)
    {
        var orderMaybe = await _orderRepository.FindByIdAsync(command.OrderId, cancellationToken);
        var orderResult = orderMaybe.ToResult(Error.NotFound($"Order {command.OrderId} not found."));
        if (orderResult.IsFailure) return orderResult;

        var order = orderResult.Value;
        var previousStatus = order.Status;

        var cancelResult = order.Cancel();
        if (cancelResult.IsFailure) return cancelResult;

        // Release stock if order was Submitted or Approved
        if (previousStatus is OrderStatus.Submitted or OrderStatus.Approved)
        {
            var productIds = order.LineItems.Select(li => li.ProductId).ToList();
            var products = await _productRepository.GetByIdsAsync(productIds, cancellationToken);

            foreach (var lineItem in order.LineItems)
            {
                var product = products.FirstOrDefault(p => p.Id == lineItem.ProductId);
                if (product is not null)
                {
                    _ = product.ReleaseStock(lineItem.Quantity);
                    _ = await _productRepository.SaveAsync(product, cancellationToken);
                }
            }
        }

        var saveResult = await _orderRepository.SaveAsync(order, cancellationToken);
        if (saveResult.IsFailure) return saveResult.Error;

        return order;
    }
}
