namespace OrderManagement.AntiCorruptionLayer;

using Microsoft.EntityFrameworkCore;
using OrderManagement.Application.Orders;
using OrderManagement.Domain;
using Trellis.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="IOrderRepository"/>.</summary>
internal sealed class OrderRepository : RepositoryBase<Order, OrderId>, IOrderRepository
{
    public OrderRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Order>> ListByCustomerAsync(CustomerId customerId, CancellationToken cancellationToken) =>
        await DbSet
            .Include(o => o.LineItems)
            .Where(o => o.CustomerId == customerId)
            .ToListAsync(cancellationToken);

    public override async Task<IReadOnlyList<Order>> QueryAsync(Specification<Order> specification, CancellationToken cancellationToken) =>
        await DbSet.Include(o => o.LineItems).Where(specification).ToListAsync(cancellationToken);

    public override async Task<Maybe<Order>> FindByIdAsync(OrderId id, CancellationToken cancellationToken)
    {
        var order = await DbSet
            .Include(o => o.LineItems)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        return order is null ? Maybe<Order>.None : Maybe.From(order);
    }
}