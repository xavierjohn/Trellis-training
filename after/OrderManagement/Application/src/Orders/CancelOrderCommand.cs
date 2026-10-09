namespace OrderManagement.Application.Orders;

using FluentValidation;
using Mediator;
using OrderManagement.Application.Products;
using OrderManagement.Domain;
using Trellis.Authorization;
using Trellis.Mediator;

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
/// receives that same loaded <see cref="Order"/> and the checked actor through
/// <see cref="ActorResourceCommandHandler{TCommand, TResource, TResponse}"/>.
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
            actor.IsOwner(resource.CreatedByActorId) || actor.HasPermission(Permissions.OrdersReadAll),
            () => Error.Forbidden.For<Order>(
                "orders.cancel.owner-or-admin", id: OrderId,
                detail: "Only the order's creator (or an actor with orders:read-all) may cancel it."));
}

public sealed class CancelOrderCommandValidator : AbstractValidator<CancelOrderCommand>
{
    public CancelOrderCommandValidator() => RuleFor(c => c.OrderId).NotNull();
}

public sealed class CancelOrderCommandHandler(IProductRepository productRepository, TimeProvider timeProvider)
    : ActorResourceCommandHandler<CancelOrderCommand, Order, Result<Order>>
{
    protected override async ValueTask<Result<Order>> Handle(
        CancelOrderCommand command, Actor actor, Order order, CancellationToken cancellationToken)
    {
        var productIds = order.LineItems.Select(li => li.ProductId).Distinct().ToList();
        var products = await productRepository.FindManyByIdAsync(productIds, cancellationToken).ConfigureAwait(false);
        var productsById = products.ToDictionary(p => p.Id);

        return order.Cancel(productsById, timeProvider).Map(_ => order);
    }
}
