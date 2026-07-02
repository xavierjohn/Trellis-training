namespace OrderManagement.Application.Orders;

using OrderManagement.Domain;

/// <summary>
/// Repository interface for <see cref="Order"/> persistence.
/// </summary>
public interface IOrderRepository
{
    /// <summary>Finds an order by id. Returns <see cref="Maybe{T}.None"/> if not found.</summary>
    Task<Maybe<Order>> FindByIdAsync(OrderId id, CancellationToken cancellationToken);

    /// <summary>
    /// Returns a bounded page of a customer's orders using forward-only cursor (keyset)
    /// pagination ordered by the order's (time-ordered) id. Used by the "list orders by
    /// customer" query. A malformed <paramref name="cursor"/> yields <c>Error.InvalidInput</c>.
    /// </summary>
    Task<Result<Page<Order>>> ListByCustomerPageAsync(
        CustomerId customerId, PageSize pageSize, Cursor? cursor, CancellationToken cancellationToken);

    /// <summary>
    /// Returns a bounded page of orders matching the specification using forward-only cursor
    /// (keyset) pagination ordered by the order's (time-ordered) id. A malformed
    /// <paramref name="cursor"/> yields <c>Error.InvalidInput</c>.
    /// </summary>
    Task<Result<Page<Order>>> QueryPageAsync(
        Specification<Order> specification, PageSize pageSize, Cursor? cursor, CancellationToken cancellationToken);

    /// <summary>Stages an aggregate for insertion. The unit-of-work commits on handler success.</summary>
    void Add(Order order);
}
