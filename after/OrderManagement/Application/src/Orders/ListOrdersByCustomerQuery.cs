namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Domain.Aggregates;
using OrderManagement.Domain.ValueObjects;
using Trellis.Authorization;
using Trellis.Mediator;

public sealed record ListOrdersByCustomerQuery(CustomerId CustomerId) : IQuery<Result<List<Order>>>, IAuthorize
{
    public IReadOnlyList<string> RequiredPermissions => [Domain.Permissions.OrdersReadAll];
}
