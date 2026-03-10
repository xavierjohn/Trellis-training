namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Domain.Aggregates;
using Trellis.Authorization;
using Trellis.Mediator;

public sealed record ListOverdueOrdersQuery() : IQuery<Result<List<Order>>>, IAuthorize
{
    public IReadOnlyList<string> RequiredPermissions => [Domain.Permissions.OrdersReadAll];
}
