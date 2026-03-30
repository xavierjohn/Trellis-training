namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;

public sealed record CancelOrderCommand(OrderId OrderId) : ICommand<Result<Order>>, IAuthorize, IAuthorizeResource<Order>
{
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersCancel];

    public IResult Authorize(Actor actor, Order resource) =>
        Result.Ensure(
            actor.IsOwner(resource.CreatedByActorId) || actor.HasPermission(Permissions.OrdersReadAll),
            Error.Forbidden("Only the order creator or an admin can cancel this order."));
}

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
        var orderResult = (await _orderRepository.FindByIdAsync(command.OrderId, cancellationToken))
            .ToResult(Error.NotFound($"Order {command.OrderId.Value} not found."));
        if (orderResult.IsFailure)
            return orderResult.Error;

        var order = orderResult.Value;
        List<Product>? products = null;

        // Load products if stock release is needed
        if (order.Status is OrderStatus.Submitted or OrderStatus.Approved)
        {
            var productIds = order.LineItems.Select(li => li.ProductId).ToList();
            products = await _productRepository.GetByIdsAsync(productIds, cancellationToken);
        }

        var cancelResult = order.Cancel(products);
        if (cancelResult.IsFailure)
            return cancelResult.Error;

        // Save products if stock was released
        if (products is not null)
        {
            foreach (var product in products)
            {
                var saveResult = await _productRepository.SaveAsync(product, cancellationToken);
                if (saveResult.IsFailure)
                    return saveResult.Error;
            }
        }

        var orderSaveResult = await _orderRepository.SaveAsync(order, cancellationToken);
        if (orderSaveResult.IsFailure)
            return orderSaveResult.Error;

        return order;
    }
}
