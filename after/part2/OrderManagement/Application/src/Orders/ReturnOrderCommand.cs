namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Application.Products;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>
/// Returns a delivered order within the 30-day return window.
/// </summary>
public sealed record ReturnOrderCommand : ICommand<Result<Order>>, IAuthorize
{
    public OrderId OrderId { get; }
    public ReturnReason Reason { get; }

    private ReturnOrderCommand(OrderId orderId, ReturnReason reason)
    {
        OrderId = orderId;
        Reason = reason;
    }

    public static Result<ReturnOrderCommand> TryCreate(OrderId orderId, ReturnReason reason, TimeProvider? timeProvider = null) =>
        new ReturnOrderCommand(orderId, reason);

    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersReturn];
}

/// <summary>
/// Handler for ReturnOrderCommand.
/// </summary>
public sealed class ReturnOrderCommandHandler : ICommandHandler<ReturnOrderCommand, Result<Order>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;

    public ReturnOrderCommandHandler(IOrderRepository orderRepository, IProductRepository productRepository)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
    }

    public async ValueTask<Result<Order>> Handle(ReturnOrderCommand command, CancellationToken cancellationToken)
    {
        var orderMaybe = await _orderRepository.FindByIdAsync(command.OrderId, cancellationToken);
        var orderResult = orderMaybe.ToResult(Error.NotFound($"Order {command.OrderId} not found."));
        if (orderResult.IsFailure) return orderResult;

        var order = orderResult.Value;

        var returnResult = order.Return(command.Reason);
        if (returnResult.IsFailure) return returnResult;

        // Release stock for each line item (same pattern as cancel)
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

        var saveResult = await _orderRepository.SaveAsync(order, cancellationToken);
        if (saveResult.IsFailure) return saveResult.Error;

        return order;
    }
}
