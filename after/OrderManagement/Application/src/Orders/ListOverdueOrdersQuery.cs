namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>
/// Lists orders that have been in Submitted status for more than 7 days without being
/// approved, as a bounded page (cursor pagination, ordered by the order's id).
/// </summary>
public sealed record ListOverdueOrdersQuery(string? Cursor, int? Limit)
    : IQuery<Result<Page<Order>>>, IAuthorize
{
    /// <inheritdoc />
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersReadAll];
}

public sealed class ListOverdueOrdersQueryHandler
    : IQueryHandler<ListOverdueOrdersQuery, Result<Page<Order>>>
{
    private readonly IOrderRepository _repository;
    private readonly TimeProvider _timeProvider;

    public ListOverdueOrdersQueryHandler(IOrderRepository repository, TimeProvider timeProvider)
    {
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public async ValueTask<Result<Page<Order>>> Handle(
        ListOverdueOrdersQuery query,
        CancellationToken cancellationToken)
    {
        var pageSize = PageSize.FromRequested(query.Limit);
        Cursor? cursor = query.Cursor is { Length: > 0 } token ? new Cursor(token) : null;
        var spec = new OverdueOrderSpecification(_timeProvider.GetUtcNow());
        return await _repository.QueryPageAsync(spec, pageSize, cursor, cancellationToken);
    }
}
