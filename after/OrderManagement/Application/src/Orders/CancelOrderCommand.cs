namespace OrderManagement.Application.Orders;

using FluentValidation;
using Mediator;
using OrderManagement.Application.Products;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>
/// Cancels an order. Permits {Draft, Submitted, Approved} → Cancelled.
/// <para>
/// Combines the static permission check (<see cref="Permissions.OrdersCancel"/>) with
/// resource-based ownership authorization (spec §5.4):
/// the actor must either be the order's <c>CreatedByActorId</c> OR hold
/// <see cref="Permissions.OrdersReadAll"/> (admin override). A non-owning, non-admin
/// caller gets <c>403 Forbidden</c>; a non-existent order surfaces as <c>404 Not Found</c>
/// from the framework's <see cref="SharedResourceLoaderById{TResource,TId}"/>.
/// </para>
/// <para>
/// Implementing <see cref="IIdentifyResource{Order, OrderId}"/> opts this command into the
/// shared loader, so we do not need a per-command <c>IResourceLoader</c>. The handler then
/// reads the same loaded <see cref="Order"/> via the v4 typed
/// <see cref="IAuthorizedResource{TMessage, TResource}"/> accessor instead of a duplicate
/// repository fetch.
/// </para>
/// </summary>
public sealed record CancelOrderCommand(OrderId OrderId)
    : ICommand<Result<Order>>,
      IAuthorize,
      IAuthorizeResource<Order>,
      IIdentifyResource<Order, OrderId>
{
    /// <inheritdoc />
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersCancel];

    /// <inheritdoc />
    public OrderId GetResourceId() => OrderId;

    /// <inheritdoc />
    public IResult Authorize(Actor actor, Order resource) =>
        Result.Ensure(
            resource.CreatedByActorId.Value == actor.Id || actor.HasPermission(Permissions.OrdersReadAll),
            new Error.Forbidden(
                PolicyId: "orders.cancel.owner-or-admin",
                Resource: ResourceRef.For<Order>(OrderId))
            { Detail = "Only the order's creator (or an actor with orders:read-all) may cancel it." });
}

public sealed class CancelOrderCommandValidator : AbstractValidator<CancelOrderCommand>
{
    public CancelOrderCommandValidator() => RuleFor(c => c.OrderId).NotNull();
}

public sealed class CancelOrderCommandHandler : ICommandHandler<CancelOrderCommand, Result<Order>>
{
    private readonly IAuthorizedResource<CancelOrderCommand, Order> _authorizedOrder;
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;
    private readonly TimeProvider _timeProvider;

    public CancelOrderCommandHandler(
        IAuthorizedResource<CancelOrderCommand, Order> authorizedOrder,
        IOrderRepository orderRepository,
        IProductRepository productRepository,
        TimeProvider timeProvider)
    {
        _authorizedOrder = authorizedOrder;
        _orderRepository = orderRepository;
        _productRepository = productRepository;
        _timeProvider = timeProvider;
    }

    public async ValueTask<Result<Order>> Handle(CancelOrderCommand command, CancellationToken cancellationToken)
    {
        // Reuse the SAME instance the resource-authorization pipeline already loaded
        // (cookbook Recipe 31). Avoids a duplicate Order load when the typed accessor
        // is populated; falls back to the repository when running under fixtures (e.g.
        // unit tests) that bypass the resource-authorization pipeline.
        if (!_authorizedOrder.TryGetResource(out var order))
        {
            var maybe = await _orderRepository.FindByIdAsync(command.OrderId, cancellationToken);
            if (!maybe.TryGetValue(out order))
                return Result.Fail<Order>(new Error.NotFound(ResourceRef.For<Order>(command.OrderId))
                { Detail = $"Order {command.OrderId} not found." });
        }

        // Cancel may release stock for Submitted/Approved orders; preload the products.
        var productIds = order.LineItems.Select(li => li.ProductId).Distinct().ToList();
        var products = await _productRepository.FindManyByIdAsync(productIds, cancellationToken);
        var productsById = products.ToDictionary(p => p.Id);

        return order.Cancel(productsById, _timeProvider).Map(_ => order);
    }
}
