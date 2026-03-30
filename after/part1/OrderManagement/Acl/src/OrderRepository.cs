namespace OrderManagement.AntiCorruptionLayer;

using Microsoft.EntityFrameworkCore;
using OrderManagement.Application;
using OrderManagement.Domain;
using Trellis.EntityFrameworkCore;

internal class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _context;

    public OrderRepository(AppDbContext context) => _context = context;

    public async Task<Maybe<Order>> FindByIdAsync(OrderId id, CancellationToken cancellationToken) =>
        await _context.Orders
            .Include(o => o.LineItems)
            .FirstOrDefaultMaybeAsync(o => o.Id == id, cancellationToken);

    public async Task<List<Order>> GetByCustomerIdAsync(CustomerId customerId, CancellationToken cancellationToken) =>
        await _context.Orders
            .Include(o => o.LineItems)
            .Where(o => o.CustomerId == customerId)
            .ToListAsync(cancellationToken);

    public async Task<List<Order>> GetOverdueAsync(DateTime asOf, CancellationToken cancellationToken)
    {
        var cutoff = asOf.AddDays(-7);
        return await _context.Orders
            .Include(o => o.LineItems)
            .Where(o => o.Status == OrderStatus.Submitted)
            .WhereLessThan(o => o.SubmittedAt, cutoff)
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<Unit>> SaveAsync(Order order, CancellationToken cancellationToken)
    {
        var entry = _context.Entry(order);
        if (entry.State == EntityState.Detached)
            _context.Orders.Add(order);

        return await _context.SaveChangesResultUnitAsync(cancellationToken);
    }
}
