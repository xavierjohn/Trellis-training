namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Domain.Aggregates;
using OrderManagement.Domain.ValueObjects;
using Trellis.Authorization;
using Trellis.Mediator;

public sealed record CancelOrderCommand(OrderId OrderId) : ICommand<Result<Order>>, IAuthorize, IAuthorizeResource<Order>
{
    public IReadOnlyList<string> RequiredPermissions => [Domain.Permissions.OrdersCancel];

    public IResult Authorize(Actor actor, Order order)
    {
        // Admin (has orders:read-all) can cancel any order
        if (actor.HasPermission(Domain.Permissions.OrdersReadAll))
        {
            return Result.Success();
        }

        // Otherwise, only the order creator can cancel
        if (actor.IsOwner(order.CreatedByActorId.Value))
        {
            return Result.Success();
        }

        return Result.Failure(Error.Forbidden("You can only cancel orders that you created."));
    }
}
