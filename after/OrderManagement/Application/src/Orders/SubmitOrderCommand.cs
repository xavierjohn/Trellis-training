namespace OrderManagement.Application.Orders;

using FluentValidation;
using Mediator;
using OrderManagement.Application.Products;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>
/// Submits a draft order. Fires the Draft → Submitted state transition and reserves
/// stock for every line item atomically. If any product has insufficient stock the
/// entire submission fails and NO stock is reserved.
/// </summary>
public sealed record SubmitOrderCommand(OrderId OrderId) : ICommand<Result<Order>>, IAuthorize
{
    /// <inheritdoc />
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersSubmit];
}

public sealed class SubmitOrderCommandValidator : AbstractValidator<SubmitOrderCommand>
{
    public SubmitOrderCommandValidator() => RuleFor(c => c.OrderId).NotNull();
}

public sealed class SubmitOrderCommandHandler : ICommandHandler<SubmitOrderCommand, Result<Order>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;
    private readonly TimeProvider _timeProvider;

    public SubmitOrderCommandHandler(
        IOrderRepository orderRepository,
        IProductRepository productRepository,
        TimeProvider timeProvider)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
        _timeProvider = timeProvider;
    }

    public async ValueTask<Result<Order>> Handle(SubmitOrderCommand command, CancellationToken cancellationToken)
    {
        var orderMaybe = await _orderRepository.FindByIdAsync(command.OrderId, cancellationToken);
        if (!orderMaybe.TryGetValue(out var order))
            return Result.Fail<Order>(new Error.NotFound(ResourceRef.For<Order>(command.OrderId))
            { Detail = $"Order {command.OrderId} not found." });

        var productIds = order.LineItems.Select(li => li.ProductId).Distinct().ToList();
        var products = await _productRepository.FindManyByIdAsync(productIds, cancellationToken);
        var productsById = products.ToDictionary(p => p.Id);

        return order.Submit(productsById, _timeProvider).Map(_ => order);
    }
}
