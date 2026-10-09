namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>
/// Lists orders that have been in Submitted status for more than 7 days without being
/// approved, as a bounded page (cursor pagination, ordered by the order's id).
/// </summary>
public sealed record ListOverdueOrdersQuery(PageRequest Pagination)
    : IQuery<Result<Page<Order>>>, IAuthorize
{
    /// <inheritdoc />
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersReadAll];
}

public sealed class ListOverdueOrdersQueryHandler(IOrderRepository repository, TimeProvider timeProvider)
    : IQueryHandler<ListOverdueOrdersQuery, Result<Page<Order>>>
{
    public ValueTask<Result<Page<Order>>> Handle(
        ListOverdueOrdersQuery query,
        CancellationToken cancellationToken) =>
        new(repository.QueryPageAsync(
            new OverdueOrderSpecification(timeProvider.GetUtcNow()), query.Pagination, cancellationToken));
}
