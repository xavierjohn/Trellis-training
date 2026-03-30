namespace OrderManagement.Application;

using OrderManagement.Domain;

public interface IOrderRepository
{
    Task<Maybe<Order>> FindByIdAsync(OrderId id, CancellationToken cancellationToken);
    Task<List<Order>> GetByCustomerIdAsync(CustomerId customerId, CancellationToken cancellationToken);
    Task<List<Order>> GetOverdueAsync(DateTime cutoff, CancellationToken cancellationToken);
    Task<Result<Unit>> SaveAsync(Order order, CancellationToken cancellationToken);
}
