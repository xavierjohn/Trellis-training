namespace OrderManagement.Domain;

using Trellis.StateMachine;

/// <summary>
/// An order placed by a customer. Has a state machine lifecycle, line items
/// (at least one required), and records the actor who created it for
/// resource-authorization ownership checks.
/// </summary>
public partial class Order : Aggregate<OrderId>
{
    private static class Triggers
    {
        public const string Submit = "Submit";
        public const string Approve = "Approve";
        public const string Ship = "Ship";
        public const string Deliver = "Deliver";
        public const string Cancel = "Cancel";
    }

    private readonly List<LineItem> _lineItems = [];
    private readonly LazyStateMachine<OrderStatus, string> _machine;

    public CustomerId CustomerId { get; private set; } = null!;
    public ActorId CreatedByActorId { get; private set; } = null!;
    public OrderStatus Status { get; private set; } = null!;

    /// <summary>When the order was submitted, or <see cref="Maybe{T}.None"/> if still Draft.</summary>
    public partial Maybe<DateTimeOffset> SubmittedAt { get; private set; }

    /// <summary>When the order was shipped, or <see cref="Maybe{T}.None"/> if not yet shipped.</summary>
    public partial Maybe<DateTimeOffset> ShippedAt { get; private set; }

    public IReadOnlyList<LineItem> LineItems => _lineItems;

    /// <summary>Sum of (Quantity * UnitPrice) across all line items.</summary>
    public decimal OrderTotal => _lineItems.Sum(li => li.LineTotal);

    /// <summary>EF Core constructor.</summary>
    private Order() : base(default!)
    {
        _machine = new LazyStateMachine<OrderStatus, string>(
            () => Status,
            s => Status = s,
            ConfigureStateMachine);
    }

    /// <summary>
    /// Creates a draft order. Caller must add at least one line item via
    /// <see cref="AddLineItem"/> before submitting.
    /// </summary>
    public Order(CustomerId customerId, ActorId createdByActorId, TimeProvider timeProvider)
        : base(OrderId.NewUniqueV7())
    {
        CustomerId = customerId;
        CreatedByActorId = createdByActorId;
        Status = OrderStatus.Draft;
        CreatedAt = timeProvider.GetUtcNow();
        SubmittedAt = Maybe<DateTimeOffset>.None;
        ShippedAt = Maybe<DateTimeOffset>.None;

        _machine = new LazyStateMachine<OrderStatus, string>(
            () => Status,
            s => Status = s,
            ConfigureStateMachine);
    }

    /// <summary>
    /// Adds a line item. Only allowed in <see cref="OrderStatus.Draft"/>. Rejects
    /// duplicate <see cref="ProductId"/> in the same order — caller must combine
    /// quantities instead.
    /// </summary>
    public Result<LineItem> AddLineItem(ProductId productId, ProductName productName, LineItemQuantity quantity, UnitPrice unitPrice)
    {
        if (Status != OrderStatus.Draft)
            return Result.Fail<LineItem>(
                Error.InvalidInput.ForRule(
                    "order.not-draft",
                    $"Cannot add a line item to an order in {Status.Value} status."));

        if (_lineItems.Any(li => li.ProductId == productId))
            return Result.Fail<LineItem>(
                Error.InvalidInput.ForRule(
                    "order.duplicate-line-item-product",
                    $"Product {productId.Value} is already in this order. Combine quantities instead."));

        var lineItem = new LineItem(productId, productName, quantity, unitPrice);
        _lineItems.Add(lineItem);
        return Result.Ok(lineItem);
    }

    /// <summary>
    /// Removes a line item by id. Order must be in Draft and must retain at least
    /// one line item (cannot remove the last one).
    /// </summary>
    public Result<Unit> RemoveLineItem(LineItemId lineItemId)
    {
        if (Status != OrderStatus.Draft)
            return Result.Fail<Unit>(
                Error.InvalidInput.ForRule(
                    "order.not-draft",
                    $"Cannot remove a line item from an order in {Status.Value} status."));

        var lineItem = _lineItems.FirstOrDefault(li => li.Id == lineItemId);
        if (lineItem is null)
            return Result.Fail<Unit>(new Error.NotFound(ResourceRef.For<LineItem>(lineItemId.Value.ToString())));

        if (_lineItems.Count == 1)
            return Result.Fail<Unit>(
                Error.InvalidInput.ForRule(
                    "order.last-line-item",
                    "Cannot remove the last line item from an order. Cancel the order instead."));

        _lineItems.Remove(lineItem);
        return Result.Ok();
    }

    /// <summary>
    /// Submits the order: Draft → Submitted. Reserves stock for each line item
    /// from the supplied product map. Fails if any product has insufficient stock;
    /// no partial reservations leak through.
    /// </summary>
    public Result<OrderStatus> Submit(IReadOnlyDictionary<ProductId, Product> products, TimeProvider timeProvider)
    {
        if (_lineItems.Count == 0)
            return Result.Fail<OrderStatus>(
                Error.InvalidInput.ForRule("order.no-line-items", "Cannot submit an order without line items."));

        // Two-phase: pre-flight check every reservation before mutating any product.
        // Stateless's Permit transition does not roll back our side effects.
        var reservations = new List<(Product Product, int Quantity)>(_lineItems.Count);
        foreach (var li in _lineItems)
        {
            if (!products.TryGetValue(li.ProductId, out var product))
                return Result.Fail<OrderStatus>(
                    new Error.NotFound(ResourceRef.For<Product>(li.ProductId.Value.ToString())));

            if (product.StockQuantity.Value < li.Quantity.Value)
                return Result.Fail<OrderStatus>(
                    Error.InvalidInput.ForRule(
                        "product.insufficient-stock",
                        $"Product '{product.ProductName.Value}' has insufficient stock: requested {li.Quantity.Value}, available {product.StockQuantity.Value}."));

            reservations.Add((product, li.Quantity.Value));
        }

        return _machine.FireResult(Triggers.Submit)
            .Bind(status =>
            {
                // Reserve stock for each line item. Pre-validated above so the failure
                // path here is "invariant violated" — surface it as a Result failure
                // rather than swallow or throw, per TRLS010.
                foreach (var (product, qty) in reservations)
                {
                    var reservation = product.ReserveStock(qty);
                    if (reservation.IsFailure)
                        return Result.Fail<OrderStatus>(reservation.Error!);
                }
                return Result.Ok(status);
            })
            .Tap(_ =>
            {
                var submittedAt = timeProvider.GetUtcNow();
                SubmittedAt = submittedAt;
                DomainEvents.Add(new OrderSubmittedEvent(Id, CustomerId, OrderTotal, submittedAt));
            });
    }

    /// <summary>Submitted → Approved.</summary>
    public Result<OrderStatus> Approve(TimeProvider timeProvider) =>
        _machine.FireResult(Triggers.Approve)
            .Tap(_ => DomainEvents.Add(new OrderApprovedEvent(Id, timeProvider.GetUtcNow())));

    /// <summary>Approved → Shipped.</summary>
    public Result<OrderStatus> Ship(TimeProvider timeProvider) =>
        _machine.FireResult(Triggers.Ship)
            .Tap(_ =>
            {
                var shippedAt = timeProvider.GetUtcNow();
                ShippedAt = shippedAt;
                DomainEvents.Add(new OrderShippedEvent(Id, CustomerId, shippedAt));
            });

    /// <summary>Shipped → Delivered.</summary>
    public Result<OrderStatus> Deliver(TimeProvider timeProvider) =>
        _machine.FireResult(Triggers.Deliver)
            .Tap(_ => DomainEvents.Add(new OrderDeliveredEvent(Id, timeProvider.GetUtcNow())));

    /// <summary>
    /// Cancels the order: {Draft, Submitted, Approved} → Cancelled. If the order
    /// was Submitted or Approved, releases the reserved stock back to each product.
    /// </summary>
    public Result<OrderStatus> Cancel(IReadOnlyDictionary<ProductId, Product> products, TimeProvider timeProvider)
    {
        var fromStatus = Status;
        var shouldReleaseStock = fromStatus == OrderStatus.Submitted || fromStatus == OrderStatus.Approved;

        // Pre-flight: if we'll release stock, every referenced product must exist.
        if (shouldReleaseStock)
        {
            foreach (var li in _lineItems)
            {
                if (!products.TryGetValue(li.ProductId, out _))
                    return Result.Fail<OrderStatus>(
                        new Error.NotFound(ResourceRef.For<Product>(li.ProductId.Value.ToString())));
            }
        }

        return _machine.FireResult(Triggers.Cancel)
            .Bind(status =>
            {
                if (!shouldReleaseStock)
                    return Result.Ok(status);

                foreach (var li in _lineItems)
                {
                    var product = products[li.ProductId];
                    var release = product.ReleaseStock(li.Quantity.Value);
                    if (release.IsFailure)
                        return Result.Fail<OrderStatus>(release.Error!);
                }
                return Result.Ok(status);
            })
            .Tap(_ => DomainEvents.Add(new OrderCancelledEvent(Id, fromStatus, timeProvider.GetUtcNow())));
    }

    private static void ConfigureStateMachine(Stateless.StateMachine<OrderStatus, string> machine)
    {
        machine.Configure(OrderStatus.Draft)
            .Permit(Triggers.Submit, OrderStatus.Submitted)
            .Permit(Triggers.Cancel, OrderStatus.Cancelled);

        machine.Configure(OrderStatus.Submitted)
            .Permit(Triggers.Approve, OrderStatus.Approved)
            .Permit(Triggers.Cancel, OrderStatus.Cancelled);

        machine.Configure(OrderStatus.Approved)
            .Permit(Triggers.Ship, OrderStatus.Shipped)
            .Permit(Triggers.Cancel, OrderStatus.Cancelled);

        machine.Configure(OrderStatus.Shipped)
            .Permit(Triggers.Deliver, OrderStatus.Delivered);

        machine.Configure(OrderStatus.Delivered);
        machine.Configure(OrderStatus.Cancelled);
    }
}
