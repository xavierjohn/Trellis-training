namespace OrderManagement.Application.Orders;

using OrderManagement.Domain;

/// <summary>
/// Repository interface for Order persistence.
/// </summary>
public interface IOrderRepository
{
    Task<Maybe<Order>> FindByIdAsync(OrderId id, CancellationToken cancellationToken);
    Task<Result<Unit>> SaveAsync(Order order, CancellationToken cancellationToken);
    Task<IReadOnlyList<Order>> GetByCustomerIdAsync(CustomerId customerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Order>> GetOverdueOrdersAsync(DateTime utcNow, CancellationToken cancellationToken);
}
