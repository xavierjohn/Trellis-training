namespace OrderManagement.Application.Queries;

using Mediator;
using Trellis.Authorization;
using Trellis.Mediator;

public sealed record GetOrderByIdQuery(OrderId OrderId) : IQuery<Result<Order>>, IAuthorize
{
    public IReadOnlyList<string> RequiredPermissions => [Permissions.OrdersRead];
}
