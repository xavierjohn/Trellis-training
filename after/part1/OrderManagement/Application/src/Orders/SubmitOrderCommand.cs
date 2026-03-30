namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;

public sealed record SubmitOrderCommand(OrderId OrderId) : ICommand<Result<Order>>, IAuthorize
{
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersSubmit];
}

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
        var orderResult = (await _orderRepository.FindByIdAsync(command.OrderId, cancellationToken))
            .ToResult(Error.NotFound($"Order {command.OrderId.Value} not found."));
        if (orderResult.IsFailure)
            return orderResult.Error;

        var order = orderResult.Value;
        var productIds = order.LineItems.Select(li => li.ProductId).ToList();
        var products = await _productRepository.GetByIdsAsync(productIds, cancellationToken);

        var submitResult = order.Submit(products);
        if (submitResult.IsFailure)
            return submitResult.Error;

        // Save products (stock was reserved) and the order
        foreach (var product in products)
        {
            var saveResult = await _productRepository.SaveAsync(product, cancellationToken);
            if (saveResult.IsFailure)
                return saveResult.Error;
        }

        var orderSaveResult = await _orderRepository.SaveAsync(order, cancellationToken);
        if (orderSaveResult.IsFailure)
            return orderSaveResult.Error;

        return order;
    }
}
