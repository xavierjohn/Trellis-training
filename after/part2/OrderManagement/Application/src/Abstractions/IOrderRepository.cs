namespace OrderManagement.Application.Abstractions;

public interface IOrderRepository
{
    Task<Result<Order>> GetByIdAsync(OrderId id, CancellationToken cancellationToken = default);
    Task<Result<Unit>> SaveAsync(Order order, CancellationToken cancellationToken = default);
    Task<Result<List<Order>>> GetByCustomerIdAsync(CustomerId customerId, CancellationToken cancellationToken = default);
    Task<Result<List<Order>>> GetOverdueOrdersAsync(CancellationToken cancellationToken = default);
}
