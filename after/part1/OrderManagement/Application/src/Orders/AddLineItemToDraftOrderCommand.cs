namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>Adds a line item to a draft order.</summary>
public sealed record AddLineItemToDraftOrderCommand(
    OrderId OrderId,
    ProductId ProductId,
    LineItemQuantity Quantity) : ICommand<Result<Order>>, IAuthorize
{
    /// <inheritdoc />
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersCreate];
}

/// <summary>Handler for <see cref="AddLineItemToDraftOrderCommand"/>.</summary>
public sealed class AddLineItemToDraftOrderCommandHandler
    : ICommandHandler<AddLineItemToDraftOrderCommand, Result<Order>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;

    /// <summary>Initializes a new instance of <see cref="AddLineItemToDraftOrderCommandHandler"/>.</summary>
    public AddLineItemToDraftOrderCommandHandler(
        IOrderRepository orderRepository,
        IProductRepository productRepository)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
    }

    /// <inheritdoc />
    public async ValueTask<Result<Order>> Handle(
        AddLineItemToDraftOrderCommand command, CancellationToken cancellationToken)
    {
        var orderResult = (await _orderRepository.FindByIdAsync(command.OrderId, cancellationToken))
            .ToResult(Error.NotFound($"Order {command.OrderId} not found.", "orderId"));
        if (orderResult.IsFailure)
            return orderResult.Error;

        var productResult = (await _productRepository.FindByIdAsync(command.ProductId, cancellationToken))
            .ToResult(Error.NotFound($"Product {command.ProductId} not found.", "productId"));
        if (productResult.IsFailure)
            return productResult.Error;

        var product = productResult.Value;
        return await orderResult.Value
            .AddLineItem(command.ProductId, product.ProductName, command.Quantity, product.UnitPrice)
            .BindAsync(order => _orderRepository.SaveAsync(order, cancellationToken).MapAsync(_ => order));
    }
}
