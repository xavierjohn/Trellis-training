namespace OrderManagement.Application.Commands;

using Mediator;
using Trellis.Authorization;
using Trellis.Mediator;

public sealed record CancelOrderCommand(OrderId OrderId)
    : ICommand<Result<Order>>, IAuthorize, IAuthorizeResource<Order>
{
    public IReadOnlyList<string> RequiredPermissions => [Permissions.OrdersCancel];

    public IResult Authorize(Actor actor, Order order)
    {
        if (actor.HasPermission(Permissions.OrdersReadAll))
            return Result.Success();
        if (actor.IsOwner(order.CreatedByActorId))
            return Result.Success();
        return Result.Failure(Error.Forbidden("You can only cancel your own orders"));
    }
}
