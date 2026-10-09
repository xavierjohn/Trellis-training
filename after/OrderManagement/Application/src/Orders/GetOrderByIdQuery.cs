namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>Gets an order by id.</summary>
public sealed record GetOrderByIdQuery(OrderId OrderId) : IQuery<Result<Order>>, IAuthorize
{
    /// <inheritdoc />
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersRead];
}

public sealed class GetOrderByIdQueryHandler(IOrderRepository repository) : IQueryHandler<GetOrderByIdQuery, Result<Order>>
{
    public async ValueTask<Result<Order>> Handle(GetOrderByIdQuery query, CancellationToken cancellationToken) =>
        await repository.FindByIdAsync(query.OrderId, cancellationToken)
            .ToResultAsync(() => Error.NotFound.For<Order>(
                id: query.OrderId, detail: $"Order {query.OrderId} not found."))
            .ConfigureAwait(false);
}
