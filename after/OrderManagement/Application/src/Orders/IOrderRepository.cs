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
    /// Returns all orders belonging to a customer. Used by the "list orders by customer" query.
    /// </summary>
    Task<IReadOnlyList<Order>> ListByCustomerAsync(CustomerId customerId, CancellationToken cancellationToken);

    /// <summary>Returns all orders matching the supplied specification.</summary>
    Task<IReadOnlyList<Order>> QueryAsync(Specification<Order> specification, CancellationToken cancellationToken);

    /// <summary>Stages an aggregate for insertion. The unit-of-work commits on handler success.</summary>
    void Add(Order order);
}
