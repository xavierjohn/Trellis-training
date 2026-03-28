namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Application.Products;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>
/// Adds a line item to a draft order.
/// </summary>
public sealed record AddLineItemCommand(
    OrderId OrderId,
    ProductId ProductId,
    LineItemQuantity Quantity) : ICommand<Result<Order>>, IAuthorize
{
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersCreate];
}

/// <summary>
/// Handler for AddLineItemCommand.
/// </summary>
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
        var orderMaybe = await _orderRepository.FindByIdAsync(command.OrderId, cancellationToken);
        var productMaybe = await _productRepository.FindByIdAsync(command.ProductId, cancellationToken);

        return await orderMaybe
            .ToResult(Error.NotFound($"Order {command.OrderId} not found."))
            .Combine(productMaybe.ToResult(Error.NotFound($"Product {command.ProductId} not found.")))
            .Bind((order, product) =>
            {
                var lineItem = LineItem.Create(command.ProductId, product.ProductName, command.Quantity, product.UnitPrice);
                return order.AddLineItem(lineItem);
            })
            .BindAsync(order => _orderRepository.SaveAsync(order, cancellationToken).MapAsync(_ => order));
    }
}
