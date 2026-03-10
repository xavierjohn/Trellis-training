namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Domain.Aggregates;
using OrderManagement.Domain.ValueObjects;
using Trellis.Authorization;
using Trellis.Mediator;

public sealed record RemoveLineItemCommand(
    OrderId OrderId,
    LineItemId LineItemId) : ICommand<Result<Order>>, IAuthorize
{
    public IReadOnlyList<string> RequiredPermissions => [Domain.Permissions.OrdersCreate];
}
