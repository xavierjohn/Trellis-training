namespace Vanilla;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// The "submit order" operation as a hurried team often writes it: one fat method, raw primitives,
/// a magic-string status check, exceptions for business failures, a per-item save, and a
/// read-modify-write on stock with no concurrency control. Every numbered BUG below is what the
/// benchmark's R1-R5 probes detect. Do not copy.
/// </summary>
public static class SubmitOrderEndpoint
{
    public static async Task<Order> SubmitAsync(AppDb db, Guid orderId, string[] actorPermissions)
    {
        // BUG 5 (authorization, R5): actorPermissions is accepted but never checked. Anyone who can
        // reach the endpoint can submit any order — there is no `orders:submit` gate.

        var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId);
        if (order is null)
            throw new KeyNotFoundException($"Order {orderId} not found.");

        // BUG 4 (state guard, R4): only an already-cancelled order is rejected. A Submitted order can
        // be submitted AGAIN, reserving its stock a second time. There is no state machine.
        if (order.Status == "Cancelled")
            throw new InvalidOperationException("Order is cancelled.");

        foreach (var item in order.Items)
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId);
            if (product is null)
                throw new KeyNotFoundException($"Product {item.ProductId} not found.");

            // BUG 3 (error mapping, R3): a business failure thrown as an exception. The endpoint's
            // catch-all turns it into HTTP 500 + a leaked internal message instead of a 4xx.
            if (product.Stock < item.Quantity)
                throw new InvalidOperationException(
                    $"Insufficient stock for {product.Name}: requested {item.Quantity}, available {product.Stock}.");

            // BUG 1 (concurrency, R1): read-modify-write with no optimistic-concurrency token — two
            // concurrent submits lost-update each other and oversell.
            // BUG 6 (invariant): raw int → stock can be driven below zero.
            product.Stock -= item.Quantity;

            // BUG 2 (atomicity, R2): saving per item means earlier items are PERSISTED even if a later
            // item fails the stock check above, leaving stock reserved for an order that never reaches
            // Submitted. There is no surrounding transaction or pre-flight check.
            await db.SaveChangesAsync();
        }

        order.Status = "Submitted";
        order.SubmittedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return order;
    }
}
