namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;

public sealed record AddLineItemCommand(OrderId OrderId, ProductId ProductId, LineItemQuantity Quantity) : ICommand<Result<Order>>, IAuthorize
{
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersCreate];
}

public sealed class AddLineItemCommandHandler : ICommandHandler<AddLineItemCommand, Result<Order>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;

    public AddLineItemCommandHandler(IOrderRepository orderRepository, IProductRepository productRepository)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
    }

    public async ValueTask<Result<Order>> Handle(AddLineItemCommand command, CancellationToken cancellationToken)
    {
        var orderResult = (await _orderRepository.FindByIdAsync(command.OrderId, cancellationToken))
            .ToResult(Error.NotFound($"Order {command.OrderId.Value} not found."));
        if (orderResult.IsFailure)
            return orderResult.Error;

        var productResult = (await _productRepository.FindByIdAsync(command.ProductId, cancellationToken))
            .ToResult(Error.NotFound($"Product {command.ProductId.Value} not found."));
        if (productResult.IsFailure)
            return productResult.Error;

        var order = orderResult.Value;
        var product = productResult.Value;

        var lineItem = new LineItem(command.ProductId, product.ProductName, command.Quantity, product.UnitPrice);
        return await order.AddLineItem(lineItem)
            .CheckAsync(_ => _orderRepository.SaveAsync(order, cancellationToken));
    }
}
