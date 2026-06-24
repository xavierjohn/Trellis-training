namespace TrellisArm;

using Trellis.StateMachine;

/// <summary>
/// A customer's order. Its lifecycle is a <see cref="LazyStateMachine{TState,TTrigger}"/>, so a
/// non-Draft order simply cannot be submitted (R4). <see cref="Submit"/> reserves stock in two
/// phases — validate every reservation, then apply — so a later shortfall never leaves an earlier
/// line drawn down (R2). Business failures come back as <see cref="Result{T}"/> values, never
/// exceptions (R3).
/// </summary>
public class Order : Aggregate<OrderId>
{
    private static class Triggers
    {
        public const string Submit = "Submit";
        public const string Cancel = "Cancel";
    }

    private readonly List<LineItem> _lineItems = [];
    private readonly LazyStateMachine<OrderStatus, string> _machine;

    public CustomerId CustomerId { get; private set; } = null!;
    public OrderStatus Status { get; private set; } = null!;
    public DateTimeOffset? SubmittedAt { get; private set; }

    public IReadOnlyList<LineItem> LineItems => _lineItems;

    /// <summary>EF Core constructor.</summary>
    private Order() : base(default!) =>
        _machine = new LazyStateMachine<OrderStatus, string>(() => Status, s => Status = s, ConfigureStateMachine);

    private Order(CustomerId customerId) : base(OrderId.NewUniqueV7())
    {
        CustomerId = customerId;
        Status = OrderStatus.Draft;
        _machine = new LazyStateMachine<OrderStatus, string>(() => Status, s => Status = s, ConfigureStateMachine);
    }

    /// <summary>Creates a draft order. Requires at least one line item with a valid quantity.</summary>
    public static Result<Order> CreateDraft(CustomerId customerId, IReadOnlyList<(ProductId ProductId, int Quantity)> items)
    {
        if (items.Count == 0)
            return Result.Fail<Order>(
                Error.InvalidInput.ForRule("order.no-line-items", "An order must have at least one line item."));

        var order = new Order(customerId);
        foreach (var (productId, quantity) in items)
        {
            var q = Quantity.TryCreate(quantity);
            if (!q.TryGetValue(out var validQuantity))
                return Result.Fail<Order>(q.Error!);

            order._lineItems.Add(new LineItem(productId, validQuantity));
        }

        return Result.Ok(order);
    }

    /// <summary>
    /// Submits the order: Draft → Submitted, reserving stock for every line atomically.
    /// </summary>
    public Result<OrderStatus> Submit(IReadOnlyDictionary<ProductId, Product> products, TimeProvider timeProvider)
    {
        // State guard: re-submitting a non-Draft order conflicts with the resource's current state
        // (409), it is not malformed input. The state machine below is the second line of defence.
        if (Status != OrderStatus.Draft)
            return Result.Fail<OrderStatus>(
                new Error.Conflict(ResourceRef.For<Order>(Id.Value), "order.not-draft")
                {
                    Detail = $"Order is {Status.Value}; only a Draft order can be submitted.",
                });

        if (_lineItems.Count == 0)
            return Result.Fail<OrderStatus>(
                Error.InvalidInput.ForRule("order.no-line-items", "Cannot submit an order without line items."));

        // Aggregate demand per product so two lines on the same product are checked together.
        var demand = _lineItems
            .GroupBy(li => li.ProductId)
            .Select(g => (ProductId: g.Key, Quantity: g.Sum(li => li.Quantity.Value)))
            .ToList();

        // Phase 1 — validate every reservation before mutating any product.
        foreach (var (productId, quantity) in demand)
        {
            if (!products.TryGetValue(productId, out var product))
                return Result.Fail<OrderStatus>(new Error.NotFound(ResourceRef.For<Product>(productId.Value)));

            if (product.Stock.Value < quantity)
                return Result.Fail<OrderStatus>(
                    Error.InvalidInput.ForRule(
                        "product.insufficient-stock",
                        $"Product '{product.Name}' has insufficient stock: requested {quantity}, available {product.Stock.Value}."));
        }

        // Phase 2 — fire the transition, then apply the pre-validated reservations.
        return _machine.FireResult(Triggers.Submit)
            .Bind(status =>
            {
                foreach (var (productId, quantity) in demand)
                {
                    var reservation = products[productId].ReserveStock(quantity);
                    if (reservation.IsFailure)
                        return Result.Fail<OrderStatus>(reservation.Error!);
                }
                return Result.Ok(status);
            })
            .Tap(_ => SubmittedAt = timeProvider.GetUtcNow());
    }

    private static void ConfigureStateMachine(Stateless.StateMachine<OrderStatus, string> machine)
    {
        machine.Configure(OrderStatus.Draft)
            .Permit(Triggers.Submit, OrderStatus.Submitted)
            .Permit(Triggers.Cancel, OrderStatus.Cancelled);

        machine.Configure(OrderStatus.Submitted);
        machine.Configure(OrderStatus.Cancelled);
    }
}
