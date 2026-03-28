namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>
/// Lists overdue orders (submitted more than 7 days ago without being approved).
/// </summary>
public sealed record ListOverdueOrdersQuery : IQuery<Result<IReadOnlyList<Order>>>, IAuthorize
{
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersReadAll];
}

/// <summary>
/// Handler for ListOverdueOrdersQuery.
/// </summary>
public sealed class ListOverdueOrdersQueryHandler : IQueryHandler<ListOverdueOrdersQuery, Result<IReadOnlyList<Order>>>
{
    private readonly IOrderRepository _orderRepository;

    public ListOverdueOrdersQueryHandler(IOrderRepository orderRepository) => _orderRepository = orderRepository;

    public async ValueTask<Result<IReadOnlyList<Order>>> Handle(ListOverdueOrdersQuery query, CancellationToken cancellationToken) =>
        Result.Success<IReadOnlyList<Order>>(
            await _orderRepository.GetOverdueOrdersAsync(DateTime.UtcNow, cancellationToken));
}
