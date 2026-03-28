namespace OrderManagement.AntiCorruptionLayer;

using OrderManagement.Application.Orders;
using OrderManagement.Domain;
using Trellis.Authorization;
using Trellis.EntityFrameworkCore;

/// <summary>
/// Resource loader for CancelOrderCommand — loads Order by ID for ownership authorization.
/// </summary>
internal class CancelOrderResourceLoader : ResourceLoaderById<CancelOrderCommand, Order, OrderId>
{
    private readonly AppDbContext _context;

    public CancelOrderResourceLoader(AppDbContext context) => _context = context;

    protected override OrderId GetId(CancelOrderCommand message) => message.OrderId;

    protected override async Task<Result<Order>> GetByIdAsync(OrderId id, CancellationToken cancellationToken) =>
        await _context.Orders
            .Where(o => o.Id == id)
            .FirstOrDefaultResultAsync(
                Error.NotFound($"Order {id.Value} not found."),
                cancellationToken)
            .ConfigureAwait(false);
}
