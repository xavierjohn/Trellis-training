namespace OrderManagement.Domain;

using OrderManagement.Domain.Events;
using Trellis.Primitives;
using Trellis.Stateless;

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

    private readonly List<LineItem> _lineItems = [];
    private readonly LazyStateMachine<string, string> _machine;

    public CustomerId CustomerId { get; private set; } = default!;
    public string CreatedByActorId { get; private set; } = default!;
    public OrderStatus Status { get; private set; } = OrderStatus.Draft;
    public DateTime CreatedAt { get; private set; }
    public partial Maybe<DateTime> SubmittedAt { get; set; }
    public partial Maybe<DateTime> ShippedAt { get; set; }
    public partial Maybe<DateTime> DeliveredAt { get; set; }
    public partial Maybe<DateTime> ReturnedAt { get; set; }

    public IReadOnlyList<LineItem> LineItems => _lineItems.AsReadOnly();

    public static Result<Order> TryCreate(
        CustomerId customerId,
        string actorId,
        List<(ProductId ProductId, string ProductName, int Quantity, Money UnitPrice)> lineItems)
    {
        if (string.IsNullOrWhiteSpace(actorId))
            return Error.Validation("Actor ID is required", "actorId");

        var order = new Order(OrderId.NewUniqueV4(), customerId, actorId);

        foreach (var (productId, productName, quantity, unitPrice) in lineItems)
        {
            var addResult = order.AddLineItemInternal(productId, productName, quantity, unitPrice);
            if (addResult.IsFailure)
                return addResult.Error;
        }

        return order;
    }

    public Result<Order> AddLineItem(ProductId productId, string productName, int quantity, Money unitPrice)
    {
        if (!Status.Is(OrderStatus.Draft))
            return Error.Conflict("Can only add line items to draft orders");

        return AddLineItemInternal(productId, productName, quantity, unitPrice);
    }

    public Result<Order> RemoveLineItem(LineItemId lineItemId)
    {
        if (!Status.Is(OrderStatus.Draft))
            return Error.Conflict("Can only remove line items from draft orders");

        var item = _lineItems.FirstOrDefault(li => li.Id == lineItemId);
        if (item is null)
            return Error.NotFound($"Line item '{lineItemId}' not found");

        _lineItems.Remove(item);
        return this;
    }

    public Result<Order> Submit(List<Product> products)
    {
        if (!Status.Is(OrderStatus.Draft))
            return Error.Conflict($"Order must be in Draft status to submit. Current status: {Status.Value}");

        if (_lineItems.Count == 0)
            return Error.Domain("Cannot submit an order with no line items");

        // Reserve stock for each line item
        foreach (var item in _lineItems)
        {
            var product = products.FirstOrDefault(p => p.Id == item.ProductId);
            if (product is null)
                return Error.NotFound($"Product '{item.ProductId}' not found");

            var reserveResult = product.ReserveStock(item.Quantity);
            if (reserveResult.TryGetError(out var reserveError))
                return reserveError;
        }

        return _machine.FireResult(Triggers.Submit)
            .Tap(_ =>
            {
                var now = DateTime.UtcNow;
                SubmittedAt = now;
                DomainEvents.Add(new OrderSubmittedEvent(Id, CustomerId, GetTotal(), now));
            })
            .Map(_ => this);
    }

    public Result<Order> Approve()
    {
        return _machine.FireResult(Triggers.Approve)
            .Tap(_ => DomainEvents.Add(new OrderApprovedEvent(Id, DateTime.UtcNow)))
            .Map(_ => this);
    }

    public Result<Order> Ship()
    {
        return _machine.FireResult(Triggers.Ship)
            .Tap(_ =>
            {
                var now = DateTime.UtcNow;
                ShippedAt = now;
                DomainEvents.Add(new OrderShippedEvent(Id, CustomerId, now));
            })
            .Map(_ => this);
    }

    public Result<Order> Deliver()
    {
        return _machine.FireResult(Triggers.Deliver)
            .Tap(_ =>
            {
                var now = DateTime.UtcNow;
                DeliveredAt = now;
                DomainEvents.Add(new OrderDeliveredEvent(Id, now));
            })
            .Map(_ => this);
    }

    public Result<Order> Cancel(List<Product>? products = null)
    {
        if (!Status.Is(OrderStatus.Draft, OrderStatus.Submitted, OrderStatus.Approved))
            return Error.Conflict($"Order cannot be cancelled from status: {Status.Value}");

        var previousStatus = Status;

        if (products is not null && Status.Is(OrderStatus.Submitted, OrderStatus.Approved))
        {
            foreach (var item in _lineItems)
            {
                var product = products.FirstOrDefault(p => p.Id == item.ProductId);
                if (product is not null)
                    _ = product.ReleaseStock(item.Quantity);
            }
        }

        return _machine.FireResult(Triggers.Cancel)
            .Tap(_ => DomainEvents.Add(new OrderCancelledEvent(Id, previousStatus, DateTime.UtcNow)))
            .Map(_ => this);
    }

    public Result<Order> Return(ReturnReason reason, List<Product> products)
    {
        if (!Status.Is(OrderStatus.Delivered))
            return Error.Conflict($"Order must be in Delivered status to return. Current status: {Status.Value}");

        if (DeliveredAt.GetValueOrDefault(DateTime.MinValue) < DateTime.UtcNow.AddDays(-30))
            return Error.Domain("Order can only be returned within 30 days of delivery");

        foreach (var item in _lineItems)
        {
            var product = products.FirstOrDefault(p => p.Id == item.ProductId);
            if (product is not null)
                _ = product.ReleaseStock(item.Quantity);
        }

        return _machine.FireResult(Triggers.Return)
            .Tap(_ =>
            {
                var now = DateTime.UtcNow;
                ReturnedAt = now;
                DomainEvents.Add(new OrderReturnedEvent(Id, CustomerId, reason, now));
            })
            .Map(_ => this);
    }

    private Result<Order> AddLineItemInternal(ProductId productId, string productName, int quantity, Money unitPrice) =>
        LineItem.TryCreate(productId, productName, quantity, unitPrice)
            .Tap(item => _lineItems.Add(item))
            .Map(_ => this);

    private Money GetTotal()
    {
        if (_lineItems.Count == 0)
            return Money.Create(0m, "USD");

        var currency = _lineItems[0].UnitPrice.Currency.Value;
        var totalAmount = _lineItems.Sum(i => i.UnitPrice.Amount * i.Quantity);
        return Money.Create(totalAmount, currency);
    }

    private static void ConfigureMachine(Stateless.StateMachine<string, string> machine)
    {
        machine.Configure(OrderStatus.Draft.Value)
            .Permit(Triggers.Submit, OrderStatus.Submitted.Value)
            .Permit(Triggers.Cancel, OrderStatus.Cancelled.Value);

        machine.Configure(OrderStatus.Submitted.Value)
            .Permit(Triggers.Approve, OrderStatus.Approved.Value)
            .Permit(Triggers.Cancel, OrderStatus.Cancelled.Value);

        machine.Configure(OrderStatus.Approved.Value)
            .Permit(Triggers.Ship, OrderStatus.Shipped.Value)
            .Permit(Triggers.Cancel, OrderStatus.Cancelled.Value);

        machine.Configure(OrderStatus.Shipped.Value)
            .Permit(Triggers.Deliver, OrderStatus.Delivered.Value);

        machine.Configure(OrderStatus.Delivered.Value)
            .Permit(Triggers.Return, OrderStatus.Returned.Value);

        machine.Configure(OrderStatus.Cancelled.Value);

        machine.Configure(OrderStatus.Returned.Value);
    }

    private Order(OrderId id, CustomerId customerId, string actorId) : base(id)
    {
        CustomerId = customerId;
        CreatedByActorId = actorId;
        CreatedAt = DateTime.UtcNow;
        Status = OrderStatus.Draft;
        _machine = new LazyStateMachine<string, string>(
            () => Status.Value,
            state => { if (OrderStatus.TryFromName(state).TryGetValue(out var s)) Status = s; },
            ConfigureMachine);
    }

    // EF Core constructor
    private Order() : base(default!)
    {
        _machine = new LazyStateMachine<string, string>(
            () => Status.Value,
            state => { if (OrderStatus.TryFromName(state).TryGetValue(out var s)) Status = s; },
            ConfigureMachine);
    }
}
