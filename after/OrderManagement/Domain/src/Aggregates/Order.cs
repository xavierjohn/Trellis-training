namespace OrderManagement.Domain.Aggregates;

using OrderManagement.Domain.Events;
using OrderManagement.Domain.ValueObjects;
using Stateless;
using Trellis.Primitives;
using Trellis.Stateless;

public partial class Order : Aggregate<OrderId>
{
    private readonly List<LineItem> _lineItems = [];

    public CustomerId CustomerId { get; private set; } = null!;
    public ActorId CreatedByActorId { get; private set; } = null!;
    public OrderStatus Status { get; private set; } = null!;
    public IReadOnlyList<LineItem> LineItems => _lineItems.AsReadOnly();
    public DateTime CreatedAt { get; private set; }
    public partial Maybe<DateTime> SubmittedAt { get; set; }
    public partial Maybe<DateTime> ShippedAt { get; set; }

    private StateMachine<string, string>? _machine;
    private StateMachine<string, string> Machine => _machine ??= ConfigureStateMachine();

    private static class Triggers
    {
        public const string Submit = nameof(Submit);
        public const string Approve = nameof(Approve);
        public const string Ship = nameof(Ship);
        public const string Deliver = nameof(Deliver);
        public const string Cancel = nameof(Cancel);
    }

    private Order() : base(default!) { }

    private StateMachine<string, string> ConfigureStateMachine()
    {
        var machine = new StateMachine<string, string>(
            () => Status.Name,
            s => Status = OrderStatus.TryFromName(s).Value);

        machine.Configure(OrderStatus.Draft.Name)
            .Permit(Triggers.Submit, OrderStatus.Submitted.Name)
            .Permit(Triggers.Cancel, OrderStatus.Cancelled.Name);

        machine.Configure(OrderStatus.Submitted.Name)
            .Permit(Triggers.Approve, OrderStatus.Approved.Name)
            .Permit(Triggers.Cancel, OrderStatus.Cancelled.Name);

        machine.Configure(OrderStatus.Approved.Name)
            .Permit(Triggers.Ship, OrderStatus.Shipped.Name)
            .Permit(Triggers.Cancel, OrderStatus.Cancelled.Name);

        machine.Configure(OrderStatus.Shipped.Name)
            .Permit(Triggers.Deliver, OrderStatus.Delivered.Name);

        machine.Configure(OrderStatus.Delivered.Name);

        machine.Configure(OrderStatus.Cancelled.Name);

        return machine;
    }

    public static Result<Order> TryCreate(
        CustomerId customerId,
        ActorId createdByActorId,
        List<LineItem> lineItems)
    {
        if (lineItems.Count == 0)
        {
            return Error.Validation("An order must have at least one line item.", "lineItems");
        }

        var order = new Order
        {
            Id = OrderId.NewUniqueV7(),
            CustomerId = customerId,
            CreatedByActorId = createdByActorId,
            Status = OrderStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };
        order._lineItems.AddRange(lineItems);

        return order;
    }

    public Result<Order> AddLineItem(LineItem lineItem)
    {
        if (!Status.Is(OrderStatus.Draft))
        {
            return Error.Validation("Can only add line items to a draft order.", "status");
        }

        if (_lineItems.Any(li => li.ProductId == lineItem.ProductId))
        {
            return Error.Validation("This product is already in the order. Combine quantities instead.", "productId");
        }

        _lineItems.Add(lineItem);
        return this;
    }

    public Result<Order> RemoveLineItem(LineItemId lineItemId)
    {
        if (!Status.Is(OrderStatus.Draft))
        {
            return Error.Validation("Can only remove line items from a draft order.", "status");
        }

        var lineItem = _lineItems.FirstOrDefault(li => li.Id == lineItemId);
        if (lineItem is null)
        {
            return Error.NotFound($"Line item '{lineItemId}' not found.");
        }

        if (_lineItems.Count <= 1)
        {
            return Error.Validation("Cannot remove the last line item from an order.", "lineItemId");
        }

        _lineItems.Remove(lineItem);
        return this;
    }

    public Result<Order> Submit(Func<ProductId, int, Result<Unit>> reserveStock)
    {
        if (_lineItems.Count == 0)
        {
            return Error.Validation("An order must have at least one line item.", "lineItems");
        }

        // Reserve stock for all line items
        foreach (var lineItem in _lineItems)
        {
            var reserveResult = reserveStock(lineItem.ProductId, lineItem.Quantity.Value);
            if (reserveResult.IsFailure)
            {
                return reserveResult.Error;
            }
        }

        var previousStatus = Status;
        var result = Machine.FireResult(Triggers.Submit);
        if (result.IsFailure)
        {
            return result.Error;
        }

        var submittedAt = DateTime.UtcNow;
        SubmittedAt = submittedAt;

        var total = CalculateTotal();
        DomainEvents.Add(new OrderSubmittedEvent(Id, CustomerId, total, submittedAt));

        return this;
    }

    public Result<Order> Approve()
    {
        return Machine.FireResult(Triggers.Approve)
            .Tap(_ => DomainEvents.Add(new OrderApprovedEvent(Id, DateTime.UtcNow)))
            .Map(_ => this);
    }

    public Result<Order> Ship()
    {
        var shippedAt = DateTime.UtcNow;
        return Machine.FireResult(Triggers.Ship)
            .Tap(_ =>
            {
                ShippedAt = shippedAt;
                DomainEvents.Add(new OrderShippedEvent(Id, CustomerId, shippedAt));
            })
            .Map(_ => this);
    }

    public Result<Order> Deliver()
    {
        return Machine.FireResult(Triggers.Deliver)
            .Tap(_ => DomainEvents.Add(new OrderDeliveredEvent(Id, DateTime.UtcNow)))
            .Map(_ => this);
    }

    public Result<Order> Cancel(Action<ProductId, int>? releaseStock = null)
    {
        var previousStatus = Status;

        var result = Machine.FireResult(Triggers.Cancel);
        if (result.IsFailure)
        {
            return result.Error;
        }

        // Release stock if order was Submitted or Approved
        if (previousStatus.Is(OrderStatus.Submitted, OrderStatus.Approved) && releaseStock is not null)
        {
            foreach (var lineItem in _lineItems)
            {
                releaseStock(lineItem.ProductId, lineItem.Quantity.Value);
            }
        }

        DomainEvents.Add(new OrderCancelledEvent(Id, previousStatus, DateTime.UtcNow));

        return this;
    }

    public Money CalculateTotal()
    {
        var total = Money.Create(0m, "USD");
        foreach (var lineItem in _lineItems)
        {
            var lineTotal = lineItem.UnitPrice.Multiply(lineItem.Quantity.Value).Value;
            total = total.Add(lineTotal).Value;
        }
        return total;
    }
}
