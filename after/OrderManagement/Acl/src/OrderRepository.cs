namespace OrderManagement.AntiCorruptionLayer;

using Microsoft.EntityFrameworkCore;
using OrderManagement.Application.Orders;
using OrderManagement.Domain;
using Trellis.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="IOrderRepository"/>.</summary>
internal sealed class OrderRepository(AppDbContext context) : RepositoryBase<Order, OrderId>(context), IOrderRepository
{
    private static readonly SeekDefinition<Order, Guid> Seek = SeekDefinition.Ascending<Order, Guid>(o => o.Id.Value);

    public Task<Result<Page<Order>>> ListByCustomerPageAsync(
        CustomerId customerId, PageRequest pagination, CancellationToken cancellationToken) =>
        DbSet
            .Include(o => o.LineItems)
            .Where(o => o.CustomerId == customerId)
            .ToPageAsync(pagination, Seek, cancellationToken: cancellationToken);

    public Task<Result<Page<Order>>> QueryPageAsync(
        Specification<Order> specification, PageRequest pagination, CancellationToken cancellationToken) =>
        DbSet
            .Include(o => o.LineItems)
            .Where(specification)
            .ToPageAsync(pagination, Seek, cancellationToken: cancellationToken);

    public override Task<Maybe<Order>> FindByIdAsync(OrderId id, CancellationToken cancellationToken) =>
        DbSet
            .Include(o => o.LineItems)
            .FirstOrDefaultMaybeAsync(o => o.Id == id, cancellationToken);
}