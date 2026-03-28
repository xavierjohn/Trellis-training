namespace OrderManagement.AntiCorruptionLayer;

using Microsoft.EntityFrameworkCore;
using OrderManagement.Application.Orders;
using OrderManagement.Domain;
using Trellis.EntityFrameworkCore;

/// <summary>
/// EF Core implementation of IOrderRepository.
/// </summary>
internal class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _context;

    public OrderRepository(AppDbContext context) => _context = context;

    public async Task<Maybe<Order>> FindByIdAsync(OrderId id, CancellationToken cancellationToken)
    {
        var entity = await _context.Orders
            .Include(o => o.LineItems)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return Maybe.From(entity);
    }

    public async Task<Result<Unit>> SaveAsync(Order order, CancellationToken cancellationToken)
    {
        var entry = _context.Entry(order);
        if (entry.State == EntityState.Detached)
            _context.Orders.Add(order);

        return await _context.SaveChangesResultUnitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Order>> GetByCustomerIdAsync(CustomerId customerId, CancellationToken cancellationToken) =>
        await _context.Orders
            .Include(o => o.LineItems)
            .Where(o => o.CustomerId == customerId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Order>> GetOverdueOrdersAsync(DateTime utcNow, CancellationToken cancellationToken)
    {
        var cutoff = utcNow.AddDays(-7);
        return await _context.Orders
            .Include(o => o.LineItems)
            .Where(o => o.Status == OrderStatus.Submitted)
            .WhereLessThan(o => o.SubmittedAt, cutoff)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
