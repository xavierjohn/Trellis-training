namespace OrderManagement.AntiCorruptionLayer;

using Microsoft.EntityFrameworkCore;
using OrderManagement.Application;
using OrderManagement.Domain;
using Trellis.EntityFrameworkCore;

/// <summary>
/// EF Core implementation of <see cref="IOrderRepository"/>.
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

    public async Task<IReadOnlyList<Order>> FindByCustomerIdAsync(CustomerId customerId, CancellationToken cancellationToken)
    {
        return await _context.Orders
            .Include(o => o.LineItems)
            .Where(o => o.CustomerId == customerId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Order>> FindAllAsync(Specification<Order> specification, CancellationToken cancellationToken)
    {
        return await _context.Orders
            .Include(o => o.LineItems)
            .Where(specification)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Result<Unit>> SaveAsync(Order order, CancellationToken cancellationToken)
    {
        var entry = _context.Entry(order);
        if (entry.State == EntityState.Detached)
            _context.Orders.Add(order);

        return await _context.SaveChangesResultUnitAsync(cancellationToken).ConfigureAwait(false);
    }
}
