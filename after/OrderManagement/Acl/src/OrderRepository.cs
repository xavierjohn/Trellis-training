namespace OrderManagement.AntiCorruptionLayer;

using Microsoft.EntityFrameworkCore;
using OrderManagement.Application.Orders;
using OrderManagement.Domain;
using Trellis.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="IOrderRepository"/>.</summary>
internal sealed class OrderRepository : RepositoryBase<Order, OrderId>, IOrderRepository
{
    public OrderRepository(AppDbContext context) : base(context) { }

    public Task<Result<Page<Order>>> ListByCustomerPageAsync(
        CustomerId customerId, PageSize pageSize, Cursor? cursor, CancellationToken cancellationToken) =>
        DbSet
            .Include(o => o.LineItems)
            .Where(o => o.CustomerId == customerId)
            .ToPageAsync(pageSize, cursor, o => o.Id.Value, cancellationToken: cancellationToken);

    public Task<Result<Page<Order>>> QueryPageAsync(
        Specification<Order> specification, PageSize pageSize, Cursor? cursor, CancellationToken cancellationToken) =>
        DbSet
            .Include(o => o.LineItems)
            .Where(specification)
            .ToPageAsync(pageSize, cursor, o => o.Id.Value, cancellationToken: cancellationToken);

    public override async Task<Maybe<Order>> FindByIdAsync(OrderId id, CancellationToken cancellationToken)
    {
        var order = await DbSet
            .Include(o => o.LineItems)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        return order is null ? Maybe<Order>.None : Maybe.From(order);
    }
}