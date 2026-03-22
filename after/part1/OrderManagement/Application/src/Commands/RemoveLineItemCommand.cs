namespace OrderManagement.Application.Commands;

using Mediator;
using Trellis.Authorization;
using Trellis.Mediator;

public sealed record RemoveLineItemCommand(OrderId OrderId, LineItemId LineItemId)
    : ICommand<Result<Order>>, IAuthorize
{
    public IReadOnlyList<string> RequiredPermissions => [Permissions.OrdersCreate];
}
