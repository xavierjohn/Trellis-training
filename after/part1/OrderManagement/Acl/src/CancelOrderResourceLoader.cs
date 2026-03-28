namespace OrderManagement.AntiCorruptionLayer;

using Microsoft.EntityFrameworkCore;
using OrderManagement.Application.Orders;
using OrderManagement.Domain;
using Trellis.Authorization;
using Trellis.EntityFrameworkCore;

/// <summary>
/// Resource loader for <see cref="CancelOrderCommand"/> — loads <see cref="Order"/> by ID for resource-based authorization.
/// </summary>
internal class CancelOrderResourceLoader : ResourceLoaderById<CancelOrderCommand, Order, OrderId>
{
    private readonly AppDbContext _context;

    public CancelOrderResourceLoader(AppDbContext context) => _context = context;

    protected override OrderId GetId(CancelOrderCommand message) => message.OrderId;

    protected override async Task<Result<Order>> GetByIdAsync(OrderId id, CancellationToken cancellationToken) =>
        await _context.Orders
            .Include(o => o.LineItems)
            .Where(o => o.Id == id)
            .FirstOrDefaultResultAsync(
                Error.NotFound($"Order {id.Value} not found.", "orderId"),
                cancellationToken)
            .ConfigureAwait(false);
}
