namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>Lists all orders belonging to a specific customer.</summary>
public sealed record ListOrdersByCustomerQuery(CustomerId CustomerId)
    : IQuery<Result<IReadOnlyList<Order>>>, IAuthorize
{
    /// <inheritdoc />
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersReadAll];
}

/// <summary>Handler for <see cref="ListOrdersByCustomerQuery"/>.</summary>
public sealed class ListOrdersByCustomerQueryHandler
    : IQueryHandler<ListOrdersByCustomerQuery, Result<IReadOnlyList<Order>>>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IOrderRepository _orderRepository;

    /// <summary>Initializes a new instance of <see cref="ListOrdersByCustomerQueryHandler"/>.</summary>
    public ListOrdersByCustomerQueryHandler(
        ICustomerRepository customerRepository,
        IOrderRepository orderRepository)
    {
        _customerRepository = customerRepository;
        _orderRepository = orderRepository;
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<Order>>> Handle(
        ListOrdersByCustomerQuery query, CancellationToken cancellationToken)
    {
        var customerResult = (await _customerRepository.FindByIdAsync(query.CustomerId, cancellationToken))
            .ToResult(Error.NotFound($"Customer {query.CustomerId} not found.", "customerId"));
        if (customerResult.IsFailure)
            return customerResult.Error;

        var orders = await _orderRepository.FindByCustomerIdAsync(query.CustomerId, cancellationToken);
        return Result.Success(orders);
    }
}
