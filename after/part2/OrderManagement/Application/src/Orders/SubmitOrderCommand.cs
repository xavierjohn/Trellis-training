namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>Submits a draft order, reserving stock for each line item.</summary>
public sealed record SubmitOrderCommand(OrderId OrderId) : ICommand<Result<Order>>, IAuthorize
{
    /// <inheritdoc />
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersSubmit];
}

/// <summary>Handler for <see cref="SubmitOrderCommand"/>.</summary>
public sealed class SubmitOrderCommandHandler : ICommandHandler<SubmitOrderCommand, Result<Order>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;

    /// <summary>Initializes a new instance of <see cref="SubmitOrderCommandHandler"/>.</summary>
    public SubmitOrderCommandHandler(IOrderRepository orderRepository, IProductRepository productRepository)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
    }

    /// <inheritdoc />
    public async ValueTask<Result<Order>> Handle(SubmitOrderCommand command, CancellationToken cancellationToken)
    {
        var orderResult = (await _orderRepository.FindByIdAsync(command.OrderId, cancellationToken))
            .ToResult(Error.NotFound($"Order {command.OrderId} not found.", "orderId"));
        if (orderResult.IsFailure)
            return orderResult.Error;

        var order = orderResult.Value;

        // Fetch all products in parallel
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
            products[i] = productResult.Value;
        }

        // Reserve stock for each line item
        for (var i = 0; i < order.LineItems.Count; i++)
        {
            var reserveResult = products[i].ReserveStock(order.LineItems[i].Quantity);
            if (reserveResult.IsFailure)
                return reserveResult.Error;
        }

        // Transition to Submitted
        var submitResult = order.Submit();
        if (submitResult.IsFailure)
            return submitResult.Error;

        // Save order and all affected products
        var saveOrderResult = await _orderRepository.SaveAsync(order, cancellationToken);
        if (saveOrderResult.IsFailure)
            return saveOrderResult.Error;

        foreach (var product in products)
        {
            var saveResult = await _productRepository.SaveAsync(product, cancellationToken);
            if (saveResult.IsFailure)
                return saveResult.Error;
        }

        return order;
    }
}
