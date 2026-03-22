namespace OrderManagement.Application.Queries;

using Mediator;
using Trellis.Authorization;
using Trellis.Mediator;

public sealed record ListOrdersByCustomerQuery(CustomerId CustomerId) : IQuery<Result<List<Order>>>, IAuthorize
{
    public IReadOnlyList<string> RequiredPermissions => [Permissions.OrdersReadAll];
}
