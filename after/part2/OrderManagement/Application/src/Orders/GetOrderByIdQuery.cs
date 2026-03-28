namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>Gets an order by ID.</summary>
public sealed record GetOrderByIdQuery(OrderId OrderId) : IQuery<Result<Order>>, IAuthorize
{
    /// <inheritdoc />
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersRead];
}

/// <summary>Handler for <see cref="GetOrderByIdQuery"/>.</summary>
public sealed class GetOrderByIdQueryHandler : IQueryHandler<GetOrderByIdQuery, Result<Order>>
{
    private readonly IOrderRepository _repository;

    /// <summary>Initializes a new instance of <see cref="GetOrderByIdQueryHandler"/>.</summary>
    public GetOrderByIdQueryHandler(IOrderRepository repository) => _repository = repository;

    /// <inheritdoc />
    public async ValueTask<Result<Order>> Handle(GetOrderByIdQuery query, CancellationToken cancellationToken) =>
        (await _repository.FindByIdAsync(query.OrderId, cancellationToken))
            .ToResult(Error.NotFound($"Order {query.OrderId} not found."));
}
