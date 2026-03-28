namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>
/// Cancels an order. Requires the actor to own the order or hold <c>orders:read-all</c>.
/// Stock is released if the order was in Submitted or Approved status.
/// </summary>
public sealed record CancelOrderCommand(OrderId OrderId) : ICommand<Result<Order>>, IAuthorize, IAuthorizeResource<Order>
{
    /// <inheritdoc />
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersCancel];

    /// <inheritdoc />
    public IResult Authorize(Actor actor, Order order) =>
        actor.Id == order.CreatedByActorId || actor.HasPermission(Permissions.OrdersReadAll)
            ? Result.Success()
            : Result.Failure(Error.Forbidden("You are not authorized to cancel this order."));
}

/// <summary>Handler for <see cref="CancelOrderCommand"/>.</summary>
public sealed class CancelOrderCommandHandler : ICommandHandler<CancelOrderCommand, Result<Order>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;

    /// <summary>Initializes a new instance of <see cref="CancelOrderCommandHandler"/>.</summary>
    public CancelOrderCommandHandler(IOrderRepository orderRepository, IProductRepository productRepository)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
    }

    /// <inheritdoc />
    public async ValueTask<Result<Order>> Handle(CancelOrderCommand command, CancellationToken cancellationToken)
    {
        var orderResult = (await _orderRepository.FindByIdAsync(command.OrderId, cancellationToken))
            .ToResult(Error.NotFound($"Order {command.OrderId} not found.", "orderId"));
        if (orderResult.IsFailure)
            return orderResult.Error;

        var order = orderResult.Value;
        var hadStockReserved = order.HadStockReserved;

        // Transition to Cancelled
        var cancelResult = order.Cancel();
        if (cancelResult.IsFailure)
            return cancelResult.Error;

        // Release reserved stock if the order was Submitted or Approved
        if (hadStockReserved)
        {
            var productTasks = order.LineItems
                .Select(li => _productRepository.FindByIdAsync(li.ProductId, cancellationToken))
                .ToArray();
            await Task.WhenAll(productTasks);

            var products = new Product[order.LineItems.Count];
            for (var i = 0; i < order.LineItems.Count; i++)
            {
                var productResult = (await productTasks[i])
                    .ToResult(Error.NotFound($"Product {order.LineItems[i].ProductId} not found.", "productId"));
                if (productResult.IsFailure)
                    return productResult.Error;

                productResult.Value.ReleaseStock(order.LineItems[i].Quantity);
                products[i] = productResult.Value;
            }

            foreach (var product in products)
            {
                var saveResult = await _productRepository.SaveAsync(product, cancellationToken);
                if (saveResult.IsFailure)
                    return saveResult.Error;
            }
        }

        return await _orderRepository.SaveAsync(order, cancellationToken).MapAsync(_ => order);
    }
}
