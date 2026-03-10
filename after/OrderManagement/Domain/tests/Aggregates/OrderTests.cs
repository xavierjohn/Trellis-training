namespace Domain.Tests.Aggregates;

using OrderManagement.Domain.Aggregates;
using OrderManagement.Domain.Events;
using OrderManagement.Domain.ValueObjects;
using Trellis.Primitives;

public class OrderTests
{
    [Fact]
    public void TryCreate_ValidInput_ReturnsSuccess()
    {
        var order = CreateDraftOrder();

        order.Status.Should().Be(OrderStatus.Draft);
        order.LineItems.Should().HaveCount(1);
        order.CustomerId.Should().NotBeNull();
        order.CreatedByActorId.Should().NotBeNull();
    }

    [Fact]
    public void TryCreate_EmptyLineItems_ReturnsFailure()
    {
        var result = Order.TryCreate(
            CustomerId.NewUniqueV7(),
            ActorId.Create("actor"),
            []);

        result.Should().BeFailure();
    }

    [Fact]
    public void AddLineItem_DraftOrder_Succeeds()
    {
        var order = CreateDraftOrder();
        var lineItem = CreateLineItem(ProductId.NewUniqueV7());

        var result = order.AddLineItem(lineItem);

        result.Should().BeSuccess();
        result.Value.LineItems.Should().HaveCount(2);
    }

    [Fact]
    public void AddLineItem_DuplicateProduct_ReturnsFailure()
    {
        var productId = ProductId.NewUniqueV7();
        var order = CreateDraftOrder(productId);
        var duplicate = CreateLineItem(productId);

        var result = order.AddLineItem(duplicate);

        result.Should().BeFailure();
    }

    [Fact]
    public void AddLineItem_NonDraftOrder_ReturnsFailure()
    {
        var order = CreateSubmittedOrder();
        var lineItem = CreateLineItem(ProductId.NewUniqueV7());

        var result = order.AddLineItem(lineItem);

        result.Should().BeFailure();
    }

    [Fact]
    public void RemoveLineItem_DraftOrder_Succeeds()
    {
        var order = CreateDraftOrder();
        order.AddLineItem(CreateLineItem(ProductId.NewUniqueV7()));
        var lineItemId = order.LineItems[0].Id;

        var result = order.RemoveLineItem(lineItemId);

        result.Should().BeSuccess();
        result.Value.LineItems.Should().HaveCount(1);
    }

    [Fact]
    public void RemoveLineItem_LastItem_ReturnsFailure()
    {
        var order = CreateDraftOrder();
        var lineItemId = order.LineItems[0].Id;

        var result = order.RemoveLineItem(lineItemId);

        result.Should().BeFailure();
    }

    [Fact]
    public void RemoveLineItem_NotFound_ReturnsFailure()
    {
        var order = CreateDraftOrder();

        var result = order.RemoveLineItem(LineItemId.NewUniqueV7());

        result.Should().BeFailure();
    }

    [Fact]
    public void Submit_DraftWithItems_TransitionsToSubmitted()
    {
        var order = CreateDraftOrder();

        var result = order.Submit((_, _) => Result.Success());

        result.Should().BeSuccess();
        result.Value.Status.Should().Be(OrderStatus.Submitted);
        result.Value.SubmittedAt.HasValue.Should().BeTrue();
    }

    [Fact]
    public void Submit_CallsReserveStockForEachItem()
    {
        var order = CreateDraftOrder();
        order.AddLineItem(CreateLineItem(ProductId.NewUniqueV7()));

        var reservedItems = new List<(ProductId, int)>();

        var result = order.Submit((pid, qty) =>
        {
            reservedItems.Add((pid, qty));
            return Result.Success();
        });

        result.Should().BeSuccess();
        reservedItems.Should().HaveCount(2);
    }

    [Fact]
    public void Submit_ReserveStockFails_ReturnsFailure()
    {
        var order = CreateDraftOrder();

        var result = order.Submit((_, _) =>
            Result.Failure<Unit>(Error.Validation("Insufficient stock", "quantity")));

        result.Should().BeFailure();
    }

    [Fact]
    public void Submit_RaisesEvents()
    {
        var order = CreateDraftOrder();

        order.Submit((_, _) => Result.Success());

        order.IsChanged.Should().BeTrue();
    }

    [Fact]
    public void Approve_SubmittedOrder_TransitionsToApproved()
    {
        var order = CreateSubmittedOrder();

        var result = order.Approve();

        result.Should().BeSuccess();
        result.Value.Status.Should().Be(OrderStatus.Approved);
    }

    [Fact]
    public void Approve_DraftOrder_ReturnsFailure()
    {
        var order = CreateDraftOrder();

        var result = order.Approve();

        result.Should().BeFailure();
    }

    [Fact]
    public void Ship_ApprovedOrder_TransitionsToShipped()
    {
        var order = CreateApprovedOrder();

        var result = order.Ship();

        result.Should().BeSuccess();
        result.Value.Status.Should().Be(OrderStatus.Shipped);
        result.Value.ShippedAt.HasValue.Should().BeTrue();
    }

    [Fact]
    public void Ship_DraftOrder_ReturnsFailure()
    {
        var order = CreateDraftOrder();

        var result = order.Ship();

        result.Should().BeFailure();
    }

    [Fact]
    public void Deliver_ShippedOrder_TransitionsToDelivered()
    {
        var order = CreateShippedOrder();

        var result = order.Deliver();

        result.Should().BeSuccess();
        result.Value.Status.Should().Be(OrderStatus.Delivered);
    }

    [Fact]
    public void Deliver_ApprovedOrder_ReturnsFailure()
    {
        var order = CreateApprovedOrder();

        var result = order.Deliver();

        result.Should().BeFailure();
    }

    [Fact]
    public void Cancel_DraftOrder_TransitionsToCancelled()
    {
        var order = CreateDraftOrder();

        var result = order.Cancel();

        result.Should().BeSuccess();
        result.Value.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void Cancel_SubmittedOrder_ReleasesStock()
    {
        var order = CreateSubmittedOrder();
        var releasedItems = new List<(ProductId, int)>();

        order.Cancel((pid, qty) => releasedItems.Add((pid, qty)));

        releasedItems.Should().HaveCount(1);
    }

    [Fact]
    public void Cancel_ApprovedOrder_Succeeds()
    {
        var order = CreateApprovedOrder();

        var result = order.Cancel();

        result.Should().BeSuccess();
        result.Value.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void Cancel_ShippedOrder_ReturnsFailure()
    {
        var order = CreateShippedOrder();

        var result = order.Cancel();

        result.Should().BeFailure();
    }

    [Fact]
    public void Cancel_DeliveredOrder_ReturnsFailure()
    {
        var order = CreateDeliveredOrder();

        var result = order.Cancel();

        result.Should().BeFailure();
    }

    [Fact]
    public void Cancel_AlreadyCancelled_ReturnsFailure()
    {
        var order = CreateDraftOrder();
        order.Cancel();

        var result = order.Cancel();

        result.Should().BeFailure();
    }

    [Fact]
    public void CalculateTotal_SingleItem_ReturnsCorrectTotal()
    {
        var order = CreateDraftOrder();

        var total = order.CalculateTotal();

        total.Amount.Should().Be(29.97m); // 3 * $9.99
    }

    // Helper methods
    private static Order CreateDraftOrder(ProductId? productId = null)
    {
        productId ??= ProductId.NewUniqueV7();
        var lineItem = CreateLineItem(productId);
        return Order.TryCreate(
            CustomerId.NewUniqueV7(),
            ActorId.Create("test-actor"),
            [lineItem]).Value;
    }

    private static Order CreateSubmittedOrder()
    {
        var order = CreateDraftOrder();
        order.Submit((_, _) => Result.Success());
        return order;
    }

    private static Order CreateApprovedOrder()
    {
        var order = CreateSubmittedOrder();
        order.Approve();
        return order;
    }

    private static Order CreateShippedOrder()
    {
        var order = CreateApprovedOrder();
        order.Ship();
        return order;
    }

    private static Order CreateDeliveredOrder()
    {
        var order = CreateShippedOrder();
        order.Deliver();
        return order;
    }

    private static LineItem CreateLineItem(ProductId productId) =>
        LineItem.TryCreate(
            productId,
            ProductName.Create("Test Product"),
            LineItemQuantity.TryCreate(3).Value,
            Money.Create(9.99m, "USD")).Value;
}
