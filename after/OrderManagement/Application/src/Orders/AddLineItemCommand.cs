namespace OrderManagement.Application.Orders;

using FluentValidation;
using Mediator;
using OrderManagement.Application.Products;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>Adds a line item to a draft order.</summary>
public sealed record AddLineItemCommand(
    OrderId OrderId, ProductId ProductId, LineItemQuantity Quantity, EntityTagValue[]? IfMatchETags = null)
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

public sealed class AddLineItemCommandHandler(IOrderRepository orderRepository, IProductRepository productRepository)
    : ICommandHandler<AddLineItemCommand, Result<Order>>
{
    public async ValueTask<Result<Order>> Handle(AddLineItemCommand command, CancellationToken cancellationToken)
    {
        var orderMaybe = await orderRepository.FindByIdAsync(command.OrderId, cancellationToken).ConfigureAwait(false);
        if (!orderMaybe.TryGetValue(out var order))
            return Result.Fail<Order>(Error.NotFound.For<Order>(
                id: command.OrderId, detail: $"Order {command.OrderId} not found."));

        // Optimistic concurrency: If-Match is required. RequireETag yields 428 when the caller
        // sent no validator and 412 when the supplied ETag no longer matches the loaded order.
        var precondition = Result.Ok(order).RequireETag(command.IfMatchETags);
        if (precondition.IsFailure)
            return precondition;

        var productMaybe = await productRepository.FindByIdAsync(command.ProductId, cancellationToken).ConfigureAwait(false);
        if (!productMaybe.TryGetValue(out var product))
            return Result.Fail<Order>(Error.NotFound.For<Product>(
                id: command.ProductId, detail: $"Product {command.ProductId} not found."));

        return order.AddLineItem(product.Id, product.ProductName, command.Quantity, product.UnitPrice)
            .Map(_ => order);
    }
}
