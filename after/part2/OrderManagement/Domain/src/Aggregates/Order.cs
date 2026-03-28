namespace OrderManagement.Domain;

using Trellis.Primitives;
using Trellis.Stateless;

/// <summary>
/// Order aggregate with state machine lifecycle management.
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
        public const string Return = "Return";
    }

    private readonly LazyStateMachine<OrderStatus, string> _machine;
    private readonly List<LineItem> _lineItems = [];

    public CustomerId CustomerId { get; private set; } = null!;
    public string CreatedByActorId { get; private set; } = null!;
    public IReadOnlyList<LineItem> LineItems => _lineItems;
    public OrderStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public partial Maybe<DateTime> SubmittedAt { get; private set; }
    public partial Maybe<DateTime> ShippedAt { get; private set; }
    public partial Maybe<DateTime> DeliveredAt { get; private set; }
    public partial Maybe<DateTime> ReturnedAt { get; private set; }

    /// <summary>
    /// Calculates the order total as sum of (unitPrice × quantity) for all line items.
    /// </summary>
    public Money Total
    {
        get
        {
            var total = Money.Create(0m, "USD");
            foreach (var item in _lineItems)
            {
                var lineTotal = item.UnitPrice.Multiply(item.Quantity.Value);
                if (lineTotal.IsSuccess)
                    total = total.Add(lineTotal.Value).Value;
            }
            return total;
        }
    }

    /// <summary>EF Core constructor.</summary>
    private Order() : base(default!)
    {
        _machine = new LazyStateMachine<OrderStatus, string>(
            () => Status,
            s => Status = s,
            ConfigureStateMachine);
    }

    private Order(CustomerId customerId, string createdByActorId, List<LineItem> lineItems)
        : base(OrderId.NewUniqueV7())
    {
        CustomerId = customerId;
        CreatedByActorId = createdByActorId;
        _lineItems = lineItems;
        Status = OrderStatus.Draft;
        CreatedAt = DateTime.UtcNow;

        _machine = new LazyStateMachine<OrderStatus, string>(
            () => Status,
            s => Status = s,
            ConfigureStateMachine);
    }

    /// <summary>
    /// Creates a new order in Draft status with line items.
    /// </summary>
    public static Result<Order> TryCreate(
        CustomerId customerId,
        string createdByActorId,
        List<LineItem> lineItems) =>
        Result.Ensure(lineItems.Count > 0, Error.Validation("An order must have at least one line item.", "lineItems"))
            .Map(_ => new Order(customerId, createdByActorId, lineItems));

    /// <summary>
    /// Adds a line item to a draft order.
    /// </summary>
    public Result<Order> AddLineItem(LineItem lineItem) =>
        Result.Ensure(Status == OrderStatus.Draft, Error.Validation("Can only add line items to a Draft order.", "status"))
            .Ensure(_ => !_lineItems.Any(li => li.ProductId == lineItem.ProductId),
                Error.Validation("This product is already in the order. Combine quantities instead.", "productId"))
            .Tap(_ => _lineItems.Add(lineItem))
            .Map(_ => this);

    /// <summary>
    /// Removes a line item from a draft order. Cannot remove the last line item.
    /// </summary>
    public Result<Order> RemoveLineItem(LineItemId lineItemId) =>
        Result.Ensure(Status == OrderStatus.Draft, Error.Validation("Can only remove line items from a Draft order.", "status"))
            .Ensure(_ => _lineItems.Count > 1, Error.Validation("Cannot remove the last line item from an order.", "lineItems"))
            .Bind(_ =>
            {
                var item = _lineItems.FirstOrDefault(li => li.Id == lineItemId);
                if (item is null)
                    return Result.Failure<Order>(Error.NotFound($"Line item {lineItemId} not found."));
                _lineItems.Remove(item);
                return Result.Success(this);
            });

    /// <summary>
    /// Submits the order (Draft → Submitted). Sets SubmittedAt.
    /// </summary>
    public Result<Order> Submit(DateTime utcNow) =>
        _machine.FireResult(Triggers.Submit)
            .Tap(_ =>
            {
                SubmittedAt = utcNow;
                DomainEvents.Add(new OrderSubmittedEvent(Id, CustomerId, Total, utcNow));
            })
            .Map(_ => this);

    /// <summary>
    /// Approves the order (Submitted → Approved).
    /// </summary>
    public Result<Order> Approve() =>
        _machine.FireResult(Triggers.Approve)
            .Tap(_ => DomainEvents.Add(new OrderApprovedEvent(Id, DateTime.UtcNow)))
            .Map(_ => this);

    /// <summary>
    /// Ships the order (Approved → Shipped). Sets ShippedAt.
    /// </summary>
    public Result<Order> Ship(DateTime utcNow) =>
        _machine.FireResult(Triggers.Ship)
            .Tap(_ =>
            {
                ShippedAt = utcNow;
                DomainEvents.Add(new OrderShippedEvent(Id, CustomerId, utcNow));
            })
            .Map(_ => this);

    /// <summary>
    /// Delivers the order (Shipped → Delivered). Sets DeliveredAt.
    /// </summary>
    public Result<Order> Deliver() =>
        _machine.FireResult(Triggers.Deliver)
            .Tap(_ =>
            {
                var now = DateTime.UtcNow;
                DeliveredAt = now;
                DomainEvents.Add(new OrderDeliveredEvent(Id, now));
            })
            .Map(_ => this);

    /// <summary>
    /// Returns the order (Delivered → Returned). Must be within 30 days of delivery.
    /// </summary>
    public Result<Order> Return(ReturnReason reason, TimeProvider? timeProvider = null)
    {
        var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;

        return DeliveredAt.Match(
            deliveredAt => Result.Ensure(
                    (now - deliveredAt).TotalDays <= 30,
                    Error.Validation("Return window has expired. Orders must be returned within 30 days of delivery.", "returnWindow"))
                .Bind(_ => _machine.FireResult(Triggers.Return))
                .Tap(_ =>
                {
                    ReturnedAt = now;
                    DomainEvents.Add(new OrderReturnedEvent(Id, CustomerId, reason, now));
                })
                .Map(_ => this),
            () => Result.Failure<Order>(Error.Validation("Order has no delivery date.", "deliveredAt")));
    }

    /// <summary>
    /// Cancels the order. Allowed from Draft, Submitted, or Approved.
    /// </summary>
    public Result<Order> Cancel() =>
        _machine.FireResult(Triggers.Cancel)
            .Tap(newStatus => DomainEvents.Add(new OrderCancelledEvent(Id, Status, DateTime.UtcNow)))
            .Map(_ => this);

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

        machine.Configure(OrderStatus.Delivered)
            .Permit(Triggers.Return, OrderStatus.Returned);

        machine.Configure(OrderStatus.Returned);
        machine.Configure(OrderStatus.Cancelled);
    }
}
