namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Application.Customers;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>Lists every order belonging to a specific customer.</summary>
public sealed record ListOrdersByCustomerQuery(CustomerId CustomerId, string? Cursor, int? Limit)
    : IQuery<Result<Page<Order>>>, IAuthorize
{
    /// <inheritdoc />
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersReadAll];
}

public sealed class ListOrdersByCustomerQueryHandler
    : IQueryHandler<ListOrdersByCustomerQuery, Result<Page<Order>>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICustomerRepository _customerRepository;

    public ListOrdersByCustomerQueryHandler(
        IOrderRepository orderRepository,
        ICustomerRepository customerRepository)
    {
        _orderRepository = orderRepository;
        _customerRepository = customerRepository;
    }

    public async ValueTask<Result<Page<Order>>> Handle(
        ListOrdersByCustomerQuery query,
        CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.FindByIdAsync(query.CustomerId, cancellationToken);
        if (!customer.TryGetValue(out _))
            return Result.Fail<Page<Order>>(new Error.NotFound(ResourceRef.For<Customer>(query.CustomerId))
            { Detail = $"Customer {query.CustomerId} not found." });

        var pageSize = PageSize.FromRequested(query.Limit);
        Cursor? cursor = query.Cursor is { Length: > 0 } token ? new Cursor(token) : null;
        return await _orderRepository.ListByCustomerPageAsync(query.CustomerId, pageSize, cursor, cancellationToken);
    }
}
