namespace OrderManagement.AntiCorruptionLayer.Repositories;

using Microsoft.EntityFrameworkCore;
using OrderManagement.Application.Abstractions;
using OrderManagement.Domain.Specifications;
using Trellis.EntityFrameworkCore;

public class OrderRepository(AppDbContext dbContext) : IOrderRepository
{
    public async Task<Result<Order>> GetByIdAsync(OrderId id, CancellationToken cancellationToken = default) =>
        await dbContext.Orders
            .Include(o => o.LineItems)
            .FirstOrDefaultResultAsync(o => o.Id == id, Error.NotFound($"Order '{id}' not found"), cancellationToken);

    public async Task<Result<Unit>> SaveAsync(Order order, CancellationToken cancellationToken = default)
    {
        dbContext.Orders.Update(order);
        return await dbContext.SaveChangesResultUnitAsync(cancellationToken);
    }

    public async Task<Result<List<Order>>> GetByCustomerIdAsync(CustomerId customerId, CancellationToken cancellationToken = default)
    {
        var orders = await dbContext.Orders
            .Include(o => o.LineItems)
            .Where(o => o.CustomerId == customerId)
            .ToListAsync(cancellationToken);
        return orders;
    }

    public async Task<Result<List<Order>>> GetOverdueOrdersAsync(CancellationToken cancellationToken = default)
    {
        var spec = new OverdueOrderSpecification();
        var orders = await dbContext.Orders
            .Include(o => o.LineItems)
            .Where(spec)
            .ToListAsync(cancellationToken);
        return orders;
    }
}
