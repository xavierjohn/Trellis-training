namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>
/// Lists all orders that are overdue: in Submitted status for more than 7 days without approval.
/// </summary>
public sealed record ListOverdueOrdersQuery : IQuery<Result<IReadOnlyList<Order>>>, IAuthorize
{
    /// <inheritdoc />
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersReadAll];
}

/// <summary>Handler for <see cref="ListOverdueOrdersQuery"/>.</summary>
public sealed class ListOverdueOrdersQueryHandler
    : IQueryHandler<ListOverdueOrdersQuery, Result<IReadOnlyList<Order>>>
{
    private readonly IOrderRepository _repository;

    /// <summary>Initializes a new instance of <see cref="ListOverdueOrdersQueryHandler"/>.</summary>
    public ListOverdueOrdersQueryHandler(IOrderRepository repository) => _repository = repository;

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<Order>>> Handle(
        ListOverdueOrdersQuery query, CancellationToken cancellationToken) =>
        Result.Success(
            await _repository.FindAllAsync(
                new OverdueOrderSpecification(DateTime.UtcNow.AddDays(-7)),
                cancellationToken));
}
