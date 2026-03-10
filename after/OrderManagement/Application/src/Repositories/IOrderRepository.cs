namespace OrderManagement.Application.Repositories;

using OrderManagement.Domain.Aggregates;
using OrderManagement.Domain.ValueObjects;

public interface IOrderRepository
{
    Task<Result<Order>> GetByIdAsync(OrderId id, CancellationToken ct);
    Task<Result<Order>> SaveAsync(Order order, CancellationToken ct);
    Task<Result<List<Order>>> GetByCustomerIdAsync(CustomerId customerId, CancellationToken ct);
    Task<Result<List<Order>>> GetOverdueOrdersAsync(DateTime utcNow, CancellationToken ct);
}
