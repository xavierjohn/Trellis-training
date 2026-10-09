namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Application.Customers;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>Lists a bounded page of orders belonging to a specific customer (cursor + limit).</summary>
public sealed record ListOrdersByCustomerQuery(CustomerId CustomerId, PageRequest Pagination)
    : IQuery<Result<Page<Order>>>, IAuthorize
{
    /// <inheritdoc />
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersReadAll];
}

public sealed class ListOrdersByCustomerQueryHandler(
    IOrderRepository orderRepository, ICustomerRepository customerRepository)
    : IQueryHandler<ListOrdersByCustomerQuery, Result<Page<Order>>>
{
    public async ValueTask<Result<Page<Order>>> Handle(
        ListOrdersByCustomerQuery query,
        CancellationToken cancellationToken)
    {
        var customer = await customerRepository.FindByIdAsync(query.CustomerId, cancellationToken).ConfigureAwait(false);
        if (!customer.TryGetValue(out _))
            return Result.Fail<Page<Order>>(Error.NotFound.For<Customer>(
                id: query.CustomerId, detail: $"Customer {query.CustomerId} not found."));

        return await orderRepository.ListByCustomerPageAsync(
            query.CustomerId, query.Pagination, cancellationToken).ConfigureAwait(false);
    }
}
