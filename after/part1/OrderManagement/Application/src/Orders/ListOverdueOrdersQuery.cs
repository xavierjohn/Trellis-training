namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;

public sealed record ListOverdueOrdersQuery : IQuery<Result<IReadOnlyList<Order>>>, IAuthorize
{
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersReadAll];
}

public sealed class ListOverdueOrdersQueryHandler : IQueryHandler<ListOverdueOrdersQuery, Result<IReadOnlyList<Order>>>
{
    private readonly IOrderRepository _repository;

    public ListOverdueOrdersQueryHandler(IOrderRepository repository) => _repository = repository;

    public async ValueTask<Result<IReadOnlyList<Order>>> Handle(ListOverdueOrdersQuery query, CancellationToken cancellationToken) =>
        Result.Success<IReadOnlyList<Order>>(
            await _repository.GetOverdueAsync(DateTime.UtcNow, cancellationToken));
}
