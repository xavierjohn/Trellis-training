namespace OrderManagement.AntiCorruptionLayer;

using Microsoft.EntityFrameworkCore;
using OrderManagement.Domain;
using Trellis.Authorization;
using Trellis.EntityFrameworkCore;

/// <summary>
/// Shared resource loader for <see cref="Order"/> authorization. Bridges the
/// pipeline's <c>IIdentifyResource&lt;Order, OrderId&gt;</c> commands to the
/// repository so <c>IAuthorizeResource&lt;Order&gt;.Authorize</c> sees a fully-loaded
/// aggregate. Includes <see cref="Order.LineItems"/> so ownership-checked mutations
/// (Cancel) can act on the same loaded instance via the v4 typed accessor.
/// </summary>
internal sealed class OrderResourceLoader : SharedResourceLoaderById<Order, OrderId>
{
    private readonly AppDbContext _context;

    public OrderResourceLoader(AppDbContext context) => _context = context;

    public override async Task<Result<Order>> GetByIdAsync(OrderId id, CancellationToken cancellationToken) =>
        await _context.Orders
            .Include(o => o.LineItems)
            .Where(o => o.Id == id)
            .FirstOrDefaultResultAsync(
                new Error.NotFound(ResourceRef.For<Order>(id)) { Detail = $"Order {id.Value} not found." },
                cancellationToken);
}