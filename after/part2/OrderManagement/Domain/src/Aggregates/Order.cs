namespace OrderManagement.Domain;

using Trellis.Primitives;
using Trellis.Stateless;

/// <summary>
/// An order placed by a customer, tracking line items and lifecycle through a state machine.
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

    private readonly LazyStateMachine<OrderStatus, string> _machine;
    private readonly List<LineItem> _lineItems = new();

    /// <summary>The customer who placed this order.</summary>
    public CustomerId CustomerId { get; private set; } = null!;

    /// <summary>The identity of the actor who created this order.</summary>
    public string CreatedByActorId { get; private set; } = null!;

    /// <summary>Current lifecycle status.</summary>
    public OrderStatus Status { get; private set; }

    /// <summary>UTC timestamp when the order was created.</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>UTC timestamp when the order was submitted, if applicable.</summary>
    public partial Maybe<DateTime> SubmittedAt { get; private set; }

    /// <summary>UTC timestamp when the order was shipped, if applicable.</summary>
    public partial Maybe<DateTime> ShippedAt { get; private set; }

    /// <summary>The line items in this order.</summary>
    public IReadOnlyList<LineItem> LineItems => _lineItems;

    /// <summary>EF Core constructor.</summary>
    private Order() : base(default!)
    {
        _machine = new LazyStateMachine<OrderStatus, string>(
            () => Status,
            s => Status = s,
            ConfigureStateMachine);
    }

    private Order(CustomerId customerId, string createdByActorId, IReadOnlyList<OrderLineItemInput> lineItems)
        : base(OrderId.NewUniqueV7())
    {
        CustomerId = customerId;
        CreatedByActorId = createdByActorId;
        Status = OrderStatus.Draft;
        CreatedAt = DateTime.UtcNow;

        _machine = new LazyStateMachine<OrderStatus, string>(
            () => Status,
            s => Status = s,
            ConfigureStateMachine);

        foreach (var item in lineItems)
            _lineItems.Add(new LineItem(LineItemId.NewUniqueV7(), item.ProductId, item.ProductName, item.Quantity, item.UnitPrice));
    }

    /// <summary>
    /// Creates a new draft order for the specified customer with the given line items.
    /// </summary>
    public static Result<Order> TryCreate(
        CustomerId customerId,
        string createdByActorId,
        IReadOnlyList<OrderLineItemInput> lineItems) =>
        Result.Ensure(lineItems.Count > 0,
                Error.Validation("Order must have at least one line item.", "lineItems"))
            .Ensure(() => lineItems.Select(li => li.ProductId).Distinct().Count() == lineItems.Count,
                Error.Validation("An order cannot contain duplicate products.", "lineItems"))
            .Map(_ => new Order(customerId, createdByActorId, lineItems));

    /// <summary>
    /// Adds a line item to a draft order. The product must not already be in the order.
    /// </summary>
    public Result<Order> AddLineItem(ProductId productId, ProductName productName, LineItemQuantity quantity, Money unitPrice) =>
        Result.Ensure(Status == OrderStatus.Draft,
                Error.Validation("Line items can only be added to a draft order.", "status"))
            .Ensure(() => _lineItems.All(li => li.ProductId != productId),
                Error.Validation("The product is already in this order.", "productId"))
            .Map(_ =>
            {
                _lineItems.Add(new LineItem(LineItemId.NewUniqueV7(), productId, productName, quantity, unitPrice));
                return this;
            });

    /// <summary>
    /// Removes a line item from a draft order. The order must have more than one line item.
    /// </summary>
    public Result<Order> RemoveLineItem(LineItemId lineItemId)
    {
        var item = _lineItems.FirstOrDefault(li => li.Id == lineItemId);
        if (item is null)
            return Error.NotFound("Line item not found.");

        return Result.Ensure(Status == OrderStatus.Draft,
                Error.Validation("Line items can only be removed from a draft order.", "status"))
            .Ensure(() => _lineItems.Count > 1,
                Error.Validation("Cannot remove the last line item from an order.", "lineItemId"))
            .Map(_ =>
            {
                _lineItems.Remove(item);
                return this;
            });
    }

    /// <summary>
    /// Submits the order, transitioning from Draft to Submitted and reserving stock.
    /// Sets SubmittedAt to current UTC time.
    /// </summary>
    public Result<Order> Submit() =>
        _machine.FireResult(Triggers.Submit)
            .Tap(_ =>
            {
                var submittedAt = DateTime.UtcNow;
                SubmittedAt = submittedAt;
                DomainEvents.Add(new OrderSubmittedEvent(Id, CustomerId, ComputeTotal(), submittedAt));
            })
            .Map(_ => this);

    /// <summary>
    /// Approves the order, transitioning from Submitted to Approved.
    /// </summary>
    public Result<Order> Approve() =>
        _machine.FireResult(Triggers.Approve)
            .Tap(_ => DomainEvents.Add(new OrderApprovedEvent(Id, DateTime.UtcNow)))
            .Map(_ => this);

    /// <summary>
    /// Ships the order, transitioning from Approved to Shipped. Sets ShippedAt to current UTC time.
    /// </summary>
    public Result<Order> Ship() =>
        _machine.FireResult(Triggers.Ship)
            .Tap(_ =>
            {
                var shippedAt = DateTime.UtcNow;
                ShippedAt = shippedAt;
                DomainEvents.Add(new OrderShippedEvent(Id, CustomerId, shippedAt));
            })
            .Map(_ => this);

    /// <summary>
    /// Delivers the order, transitioning from Shipped to Delivered.
    /// </summary>
    public Result<Order> Deliver() =>
        _machine.FireResult(Triggers.Deliver)
            .Tap(_ => DomainEvents.Add(new OrderDeliveredEvent(Id, DateTime.UtcNow)))
            .Map(_ => this);

    /// <summary>
    /// Cancels the order. Valid from Draft, Submitted, or Approved status.
    /// If the order was Submitted or Approved, the caller should release reserved stock.
    /// </summary>
    public Result<Order> Cancel()
    {
        var cancelledFromStatus = Status;
        return _machine.FireResult(Triggers.Cancel)
            .Tap(_ => DomainEvents.Add(new OrderCancelledEvent(Id, cancelledFromStatus, DateTime.UtcNow)))
            .Map(_ => this);
    }

    /// <summary>Computes the order total as the sum of all line item prices.</summary>
    public Money ComputeTotal() =>
        _lineItems.Aggregate(
            Money.Zero("USD").Value,
            (acc, li) => acc.Add(li.UnitPrice.Multiply(li.Quantity.Value).Value).Value);

    /// <summary>
    /// Returns true if this order had stock reserved (Submitted or Approved),
    /// so the caller can release that stock when cancelling.
    /// </summary>
    public bool HadStockReserved =>
        Status is OrderStatus.Submitted or OrderStatus.Approved;

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
