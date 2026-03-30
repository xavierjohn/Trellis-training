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

    public static Result<Order> TryCreate(
        CustomerId customerId,
        string createdByActorId,
        List<LineItem> lineItems)
    {
        if (lineItems.Count == 0)
            return Error.Validation("Order must have at least one line item.", "lineItems");

        var duplicateProductIds = lineItems.GroupBy(li => li.ProductId).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicateProductIds.Count > 0)
            return Error.Validation("Duplicate products are not allowed in the same order.", "lineItems");

        return new Order(customerId, createdByActorId, lineItems);
    }

    public Result<Order> AddLineItem(LineItem lineItem)
    {
        if (Status != OrderStatus.Draft)
            return Error.Validation("Can only add line items to a draft order.", "status");

        if (_lineItems.Any(li => li.ProductId == lineItem.ProductId))
            return Error.Validation("Product already exists in the order.", "productId");

        _lineItems.Add(lineItem);
        return this;
    }

    public Result<Order> RemoveLineItem(LineItemId lineItemId)
    {
        if (Status != OrderStatus.Draft)
            return Error.Validation("Can only remove line items from a draft order.", "status");

        var lineItem = _lineItems.FirstOrDefault(li => li.Id == lineItemId);
        if (lineItem is null)
            return Error.NotFound($"Line item {lineItemId.Value} not found.");

        if (_lineItems.Count <= 1)
            return Error.Validation("Cannot remove the last line item from an order.", "lineItems");

        _lineItems.Remove(lineItem);
        return this;
    }

    public Result<Order> Submit(List<Product> products)
    {
        if (_lineItems.Count == 0)
            return Error.Validation("Order must have at least one line item.", "lineItems");

        // Check stock and reserve
        foreach (var lineItem in _lineItems)
        {
            var product = products.FirstOrDefault(p => p.Id == lineItem.ProductId);
            if (product is null)
                return Error.NotFound($"Product {lineItem.ProductId.Value} not found.");

            var stockQtyResult = StockQuantity.TryCreate(lineItem.Quantity.Value);
            if (!stockQtyResult.TryGetValue(out var stockQty))
                return stockQtyResult.Error;

            var reserveResult = product.ReserveStock(stockQty);
            if (reserveResult.IsFailure)
                return reserveResult.Error;
        }

        return _machine.FireResult(Triggers.Submit)
            .Map(_ =>
            {
                var now = DateTime.UtcNow;
                SubmittedAt = now;
                DomainEvents.Add(new OrderSubmittedEvent(Id, CustomerId, CalculateTotal(), now));
                return this;
            });
    }

    public Result<Order> Approve() =>
        _machine.FireResult(Triggers.Approve)
            .Map(_ =>
            {
                DomainEvents.Add(new OrderApprovedEvent(Id, DateTime.UtcNow));
                return this;
            });

    public Result<Order> Ship() =>
        _machine.FireResult(Triggers.Ship)
            .Map(_ =>
            {
                var now = DateTime.UtcNow;
                ShippedAt = now;
                DomainEvents.Add(new OrderShippedEvent(Id, CustomerId, now));
                return this;
            });

    public Result<Order> Deliver() =>
        _machine.FireResult(Triggers.Deliver)
            .Map(_ =>
            {
                DomainEvents.Add(new OrderDeliveredEvent(Id, DateTime.UtcNow));
                return this;
            });

    public Result<Order> Cancel(List<Product>? products = null)
    {
        var previousStatus = Status;

        var fireResult = _machine.FireResult(Triggers.Cancel);
        if (fireResult.IsFailure)
            return fireResult.Error;

        // Release stock if was Submitted or Approved
        if (previousStatus is OrderStatus.Submitted or OrderStatus.Approved && products is not null)
        {
            foreach (var lineItem in _lineItems)
            {
                var product = products.FirstOrDefault(p => p.Id == lineItem.ProductId);
                if (product is not null)
                {
                    var stockQty = StockQuantity.Create(lineItem.Quantity.Value);
#pragma warning disable TRLS001
                    _ = product.ReleaseStock(stockQty);
#pragma warning restore TRLS001
                }
            }
        }

        DomainEvents.Add(new OrderCancelledEvent(Id, previousStatus, DateTime.UtcNow));
        return this;
    }

    public Money CalculateTotal()
    {
        var total = Money.Zero("USD").Value;
        foreach (var lineItem in _lineItems)
        {
            var lineTotal = lineItem.UnitPrice.Multiply(lineItem.Quantity.Value);
            if (lineTotal.TryGetValue(out var lt))
            {
                var sum = total.Add(lt);
                if (sum.TryGetValue(out var s))
                    total = s;
            }
        }
        return total;
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
