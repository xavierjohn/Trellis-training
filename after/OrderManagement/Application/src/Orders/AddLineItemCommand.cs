namespace OrderManagement.Application.Orders;

using FluentValidation;
using Mediator;
using OrderManagement.Application.Products;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>Adds a line item to a draft order.</summary>
public sealed record AddLineItemCommand(OrderId OrderId, ProductId ProductId, LineItemQuantity Quantity)
    : ICommand<Result<Order>>, IAuthorize
{
    /// <inheritdoc />
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersCreate];
}

public sealed class AddLineItemCommandValidator : AbstractValidator<AddLineItemCommand>
{
    public AddLineItemCommandValidator()
    {
        RuleFor(c => c.OrderId).NotNull();
        RuleFor(c => c.ProductId).NotNull();
        RuleFor(c => c.Quantity).NotNull();
    }
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
        var orderMaybe = await _orderRepository.FindByIdAsync(command.OrderId, cancellationToken);
        if (!orderMaybe.TryGetValue(out var order))
            return Result.Fail<Order>(new Error.NotFound(ResourceRef.For<Order>(command.OrderId))
            { Detail = $"Order {command.OrderId} not found." });

        var productMaybe = await _productRepository.FindByIdAsync(command.ProductId, cancellationToken);
        if (!productMaybe.TryGetValue(out var product))
            return Result.Fail<Order>(new Error.NotFound(ResourceRef.For<Product>(command.ProductId))
            { Detail = $"Product {command.ProductId} not found." });

        return order.AddLineItem(product.Id, product.ProductName, command.Quantity, product.UnitPrice)
            .Map(_ => order);
    }
}
