namespace OrderManagement.Application;

using OrderManagement.Domain;

/// <summary>Repository interface for Order persistence.</summary>
public interface IOrderRepository
{
    /// <summary>Finds an order by ID. Returns Maybe.None if not found.</summary>
    Task<Maybe<Order>> FindByIdAsync(OrderId id, CancellationToken cancellationToken);

    /// <summary>Returns all orders for the specified customer.</summary>
    Task<IReadOnlyList<Order>> FindByCustomerIdAsync(CustomerId customerId, CancellationToken cancellationToken);

    /// <summary>Returns all orders matching the specification.</summary>
    Task<IReadOnlyList<Order>> FindAllAsync(Specification<Order> specification, CancellationToken cancellationToken);

    /// <summary>Saves a new or updated order.</summary>
    Task<Result<Unit>> SaveAsync(Order order, CancellationToken cancellationToken);
}
