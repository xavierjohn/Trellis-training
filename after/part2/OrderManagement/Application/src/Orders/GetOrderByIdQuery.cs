namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>
/// Gets an order by ID.
/// </summary>
public sealed record GetOrderByIdQuery(OrderId OrderId) : IQuery<Result<Order>>, IAuthorize
{
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersRead];
}

/// <summary>
/// Handler for GetOrderByIdQuery.
/// </summary>
public sealed class GetOrderByIdQueryHandler : IQueryHandler<GetOrderByIdQuery, Result<Order>>
{
    private readonly IOrderRepository _orderRepository;

    public GetOrderByIdQueryHandler(IOrderRepository orderRepository) => _orderRepository = orderRepository;

    public async ValueTask<Result<Order>> Handle(GetOrderByIdQuery query, CancellationToken cancellationToken) =>
        (await _orderRepository.FindByIdAsync(query.OrderId, cancellationToken))
            .ToResult(Error.NotFound($"Order {query.OrderId} not found."));
}
