namespace OrderManagement.Application.Commands;

using Mediator;
using Trellis.Authorization;
using Trellis.Mediator;

public sealed record ShipOrderCommand(OrderId OrderId) : ICommand<Result<Order>>, IAuthorize
{
    public IReadOnlyList<string> RequiredPermissions => [Permissions.OrdersShip];
}
