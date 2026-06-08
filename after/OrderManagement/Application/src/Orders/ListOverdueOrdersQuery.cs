namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>
/// Lists every order that has been in Submitted status for more than 7 days
/// without being approved.
/// </summary>
public sealed record ListOverdueOrdersQuery : IQuery<Result<IReadOnlyList<Order>>>, IAuthorize
{
    /// <inheritdoc />
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersReadAll];
}

public sealed class ListOverdueOrdersQueryHandler
    : IQueryHandler<ListOverdueOrdersQuery, Result<IReadOnlyList<Order>>>
{
    private readonly IOrderRepository _repository;
    private readonly TimeProvider _timeProvider;

    public ListOverdueOrdersQueryHandler(IOrderRepository repository, TimeProvider timeProvider)
    {
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public async ValueTask<Result<IReadOnlyList<Order>>> Handle(
        ListOverdueOrdersQuery query,
        CancellationToken cancellationToken)
    {
        var spec = new OverdueOrderSpecification(_timeProvider.GetUtcNow());
        var orders = await _repository.QueryAsync(spec, cancellationToken);
        return Result.Ok(orders);
    }
}
