namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Application.Customers;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>
/// Lists orders for a customer.
/// </summary>
public sealed record ListOrdersByCustomerQuery(CustomerId CustomerId) : IQuery<Result<IReadOnlyList<Order>>>, IAuthorize
{
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersReadAll];
}

/// <summary>
/// Handler for ListOrdersByCustomerQuery.
/// </summary>
public sealed class ListOrdersByCustomerQueryHandler : IQueryHandler<ListOrdersByCustomerQuery, Result<IReadOnlyList<Order>>>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IOrderRepository _orderRepository;

    public ListOrdersByCustomerQueryHandler(ICustomerRepository customerRepository, IOrderRepository orderRepository)
    {
        _customerRepository = customerRepository;
        _orderRepository = orderRepository;
    }

    public async ValueTask<Result<IReadOnlyList<Order>>> Handle(ListOrdersByCustomerQuery query, CancellationToken cancellationToken)
    {
        var customerMaybe = await _customerRepository.FindByIdAsync(query.CustomerId, cancellationToken);
        var customerResult = customerMaybe.ToResult(Error.NotFound($"Customer {query.CustomerId} not found."));
        if (customerResult.IsFailure)
        {
            _ = customerResult.TryGetError(out var error);
            return error!;
        }

        var orders = await _orderRepository.GetByCustomerIdAsync(query.CustomerId, cancellationToken);
        return Result.Success<IReadOnlyList<Order>>(orders);
    }
}
