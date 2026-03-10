namespace OrderManagement.AntiCorruptionLayer.Repositories;

using Microsoft.EntityFrameworkCore;
using OrderManagement.Application.Repositories;
using OrderManagement.Domain.Aggregates;
using OrderManagement.Domain.Specifications;
using OrderManagement.Domain.ValueObjects;
using Trellis.EntityFrameworkCore;

public class OrderRepository(OrderManagementDbContext dbContext) : IOrderRepository
{
    public async Task<Result<Order>> GetByIdAsync(OrderId id, CancellationToken ct) =>
        await dbContext.Orders
            .Include(o => o.LineItems)
            .FirstOrDefaultResultAsync(o => o.Id == id, Error.NotFound($"Order '{id}' not found."), ct);

    public async Task<Result<Order>> SaveAsync(Order order, CancellationToken ct)
    {
        var entry = dbContext.Entry(order);
        if (entry.State == EntityState.Detached)
        {
            dbContext.Orders.Add(order);
        }

        return await dbContext.SaveChangesResultUnitAsync(ct)
            .MapAsync(_ => order);
    }

    public async Task<Result<List<Order>>> GetByCustomerIdAsync(CustomerId customerId, CancellationToken ct)
    {
        var orders = await dbContext.Orders
            .Include(o => o.LineItems)
            .Where(o => o.CustomerId == customerId)
            .ToListAsync(ct);

        return Result.Success(orders);
    }

    public async Task<Result<List<Order>>> GetOverdueOrdersAsync(DateTime utcNow, CancellationToken ct)
    {
        var spec = new OverdueOrderSpecification(utcNow);
        var orders = await dbContext.Orders
            .Include(o => o.LineItems)
            .Where(spec)
            .ToListAsync(ct);

        return Result.Success(orders);
    }
}
