namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Application.Products;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>
/// Submits an order (Draft → Submitted), reserving stock for each line item.
/// </summary>
public sealed record SubmitOrderCommand(OrderId OrderId) : ICommand<Result<Order>>, IAuthorize
{
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersSubmit];
}

/// <summary>
/// Handler for SubmitOrderCommand.
/// </summary>
public sealed class SubmitOrderCommandHandler : ICommandHandler<SubmitOrderCommand, Result<Order>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;

    public SubmitOrderCommandHandler(IOrderRepository orderRepository, IProductRepository productRepository)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
    }

    public async ValueTask<Result<Order>> Handle(SubmitOrderCommand command, CancellationToken cancellationToken)
    {
        var orderMaybe = await _orderRepository.FindByIdAsync(command.OrderId, cancellationToken);
        var orderResult = orderMaybe.ToResult(Error.NotFound($"Order {command.OrderId} not found."));
        if (orderResult.IsFailure) return orderResult;

        var order = orderResult.Value;

        // Reserve stock for each line item
        var productIds = order.LineItems.Select(li => li.ProductId).ToList();
        var products = await _productRepository.GetByIdsAsync(productIds, cancellationToken);

        foreach (var lineItem in order.LineItems)
        {
            var product = products.FirstOrDefault(p => p.Id == lineItem.ProductId);
            if (product is null)
                return Error.NotFound($"Product {lineItem.ProductId} not found.");

            var reserveResult = product.ReserveStock(lineItem.Quantity);
            if (reserveResult.IsFailure) return reserveResult.Error;
        }

        var submitResult = order.Submit(DateTime.UtcNow);
        if (submitResult.IsFailure) return submitResult;

        // Save products with reserved stock
        foreach (var product in products)
        {
            var saveResult = await _productRepository.SaveAsync(product, cancellationToken);
            if (saveResult.IsFailure) return saveResult.Error;
        }

        var orderSaveResult = await _orderRepository.SaveAsync(order, cancellationToken);
        if (orderSaveResult.IsFailure) return orderSaveResult.Error;

        return order;
    }
}
