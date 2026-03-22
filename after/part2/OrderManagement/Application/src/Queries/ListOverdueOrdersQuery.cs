namespace OrderManagement.Application.Queries;

using Mediator;
using Trellis.Authorization;
using Trellis.Mediator;

public sealed record ListOverdueOrdersQuery : IQuery<Result<List<Order>>>, IAuthorize
{
    public IReadOnlyList<string> RequiredPermissions => [Permissions.OrdersReadAll];
}
