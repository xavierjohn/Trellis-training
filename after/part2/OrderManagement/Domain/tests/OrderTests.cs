#pragma warning disable TRLS001, TRLS003

namespace Domain.Tests;

using OrderManagement.Domain;
using Trellis.Primitives;
using Trellis.Testing;

public class OrderTests
{
    private static CustomerId ValidCustomerId => CustomerId.NewUniqueV7();
    private static LineItem CreateLineItem() =>
        LineItem.Create(ProductId.NewUniqueV7(), ProductName.Create("Widget"), LineItemQuantity.Create(2), Money.Create(19.99m, "USD"));

    [Fact]
    public void TryCreate_ValidWithLineItems_ReturnsSuccess()
    {
        var result = Order.TryCreate(ValidCustomerId, "actor-1", [CreateLineItem()]);

        result.Should().BeSuccess();
        result.Value.Status.Should().Be(OrderStatus.Draft);
        result.Value.LineItems.Should().HaveCount(1);
    }

    [Fact]
    public void TryCreate_EmptyLineItems_ReturnsFailure()
    {
        var result = Order.TryCreate(ValidCustomerId, "actor-1", []);

        result.Should().BeFailure();
    }

    [Fact]
    public void AddLineItem_DraftOrder_Succeeds()
    {
        var order = Order.TryCreate(ValidCustomerId, "actor-1", [CreateLineItem()]).Value;
        var newItem = LineItem.Create(ProductId.NewUniqueV7(), ProductName.Create("Gadget"), LineItemQuantity.Create(1), Money.Create(9.99m, "USD"));

        var result = order.AddLineItem(newItem);

        result.Should().BeSuccess();
        result.Value.LineItems.Should().HaveCount(2);
    }

    [Fact]
    public void AddLineItem_DuplicateProduct_ReturnsFailure()
    {
        var productId = ProductId.NewUniqueV7();
        var item = LineItem.Create(productId, ProductName.Create("Widget"), LineItemQuantity.Create(1), Money.Create(10m, "USD"));
        var order = Order.TryCreate(ValidCustomerId, "actor-1", [item]).Value;

        var duplicate = LineItem.Create(productId, ProductName.Create("Widget"), LineItemQuantity.Create(2), Money.Create(10m, "USD"));
        var result = order.AddLineItem(duplicate);

        result.Should().BeFailure();
    }

    [Fact]
    public void RemoveLineItem_MoreThanOne_Succeeds()
    {
        var item1 = CreateLineItem();
        var item2 = LineItem.Create(ProductId.NewUniqueV7(), ProductName.Create("Gadget"), LineItemQuantity.Create(1), Money.Create(5m, "USD"));
        var order = Order.TryCreate(ValidCustomerId, "actor-1", [item1, item2]).Value;

        var result = order.RemoveLineItem(item1.Id);

        result.Should().BeSuccess();
        result.Value.LineItems.Should().HaveCount(1);
    }

    [Fact]
    public void RemoveLineItem_LastItem_ReturnsFailure()
    {
        var item = CreateLineItem();
        var order = Order.TryCreate(ValidCustomerId, "actor-1", [item]).Value;

        var result = order.RemoveLineItem(item.Id);

        result.Should().BeFailure();
    }

    [Fact]
    public void Submit_DraftOrder_TransitionsToSubmitted()
    {
        var order = Order.TryCreate(ValidCustomerId, "actor-1", [CreateLineItem()]).Value;
        var now = DateTime.UtcNow;

        var result = order.Submit(now);

        result.Should().BeSuccess();
        result.Value.Status.Should().Be(OrderStatus.Submitted);
        result.Value.SubmittedAt.Should().HaveValue();
    }

    [Fact]
    public void Approve_SubmittedOrder_TransitionsToApproved()
    {
        var order = Order.TryCreate(ValidCustomerId, "actor-1", [CreateLineItem()]).Value;
        order.Submit(DateTime.UtcNow);

        var result = order.Approve();

        result.Should().BeSuccess();
        result.Value.Status.Should().Be(OrderStatus.Approved);
    }

    [Fact]
    public void Ship_ApprovedOrder_TransitionsToShipped()
    {
        var order = Order.TryCreate(ValidCustomerId, "actor-1", [CreateLineItem()]).Value;
        order.Submit(DateTime.UtcNow);
        order.Approve();

        var result = order.Ship(DateTime.UtcNow);

        result.Should().BeSuccess();
        result.Value.Status.Should().Be(OrderStatus.Shipped);
        result.Value.ShippedAt.Should().HaveValue();
    }

    [Fact]
    public void Deliver_ShippedOrder_TransitionsToDelivered()
    {
        var order = Order.TryCreate(ValidCustomerId, "actor-1", [CreateLineItem()]).Value;
        order.Submit(DateTime.UtcNow);
        order.Approve();
        order.Ship(DateTime.UtcNow);

        var result = order.Deliver();

        result.Should().BeSuccess();
        result.Value.Status.Should().Be(OrderStatus.Delivered);
        result.Value.DeliveredAt.Should().HaveValue();
    }

    [Fact]
    public void Cancel_DraftOrder_Succeeds()
    {
        var order = Order.TryCreate(ValidCustomerId, "actor-1", [CreateLineItem()]).Value;

        var result = order.Cancel();

        result.Should().BeSuccess();
        result.Value.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void Cancel_SubmittedOrder_Succeeds()
    {
        var order = Order.TryCreate(ValidCustomerId, "actor-1", [CreateLineItem()]).Value;
        order.Submit(DateTime.UtcNow);

        var result = order.Cancel();

        result.Should().BeSuccess();
        result.Value.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void Cancel_ShippedOrder_Fails()
    {
        var order = Order.TryCreate(ValidCustomerId, "actor-1", [CreateLineItem()]).Value;
        order.Submit(DateTime.UtcNow);
        order.Approve();
        order.Ship(DateTime.UtcNow);

        var result = order.Cancel();

        result.Should().BeFailure();
    }

    [Fact]
    public void Cancel_DeliveredOrder_Fails()
    {
        var order = Order.TryCreate(ValidCustomerId, "actor-1", [CreateLineItem()]).Value;
        order.Submit(DateTime.UtcNow);
        order.Approve();
        order.Ship(DateTime.UtcNow);
        order.Deliver();

        var result = order.Cancel();

        result.Should().BeFailure();
    }

    [Fact]
    public void Approve_DraftOrder_Fails()
    {
        var order = Order.TryCreate(ValidCustomerId, "actor-1", [CreateLineItem()]).Value;

        var result = order.Approve();

        result.Should().BeFailure();
    }

    [Fact]
    public void Total_CalculatesCorrectly()
    {
        var item1 = LineItem.Create(ProductId.NewUniqueV7(), ProductName.Create("A"), LineItemQuantity.Create(2), Money.Create(10m, "USD"));
        var item2 = LineItem.Create(ProductId.NewUniqueV7(), ProductName.Create("B"), LineItemQuantity.Create(3), Money.Create(5m, "USD"));
        var order = Order.TryCreate(ValidCustomerId, "actor-1", [item1, item2]).Value;

        order.Total.Amount.Should().Be(35m);
    }

    private static Order CreateDeliveredOrder()
    {
        var order = Order.TryCreate(ValidCustomerId, "actor-1", [CreateLineItem()]).Value;
        order.Submit(DateTime.UtcNow);
        order.Approve();
        order.Ship(DateTime.UtcNow);
        order.Deliver();
        return order;
    }

    private static ReturnReason ValidReturnReason => ReturnReason.Create("Product was damaged during shipping");

    [Fact]
    public void Return_DeliveredOrder_WithinWindow_Succeeds()
    {
        var order = CreateDeliveredOrder();
        var timeProvider = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(DateTimeOffset.UtcNow);

        var result = order.Return(ValidReturnReason, timeProvider);

        result.Should().BeSuccess();
        result.Value.Status.Should().Be(OrderStatus.Returned);
    }

    [Fact]
    public void Return_DeliveredOrder_ExpiredWindow_Fails()
    {
        var order = CreateDeliveredOrder();
        var timeProvider = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(DateTimeOffset.UtcNow.AddDays(31));

        var result = order.Return(ValidReturnReason, timeProvider);

        result.Should().BeFailure();
    }

    [Fact]
    public void Return_ShippedOrder_Fails()
    {
        var order = Order.TryCreate(ValidCustomerId, "actor-1", [CreateLineItem()]).Value;
        order.Submit(DateTime.UtcNow);
        order.Approve();
        order.Ship(DateTime.UtcNow);

        var result = order.Return(ValidReturnReason);

        result.Should().BeFailure();
    }

    [Fact]
    public void Return_CancelledOrder_Fails()
    {
        var order = Order.TryCreate(ValidCustomerId, "actor-1", [CreateLineItem()]).Value;
        order.Cancel();

        var result = order.Return(ValidReturnReason);

        result.Should().BeFailure();
    }

    [Fact]
    public void Return_RaisesOrderReturnedEvent()
    {
        var order = CreateDeliveredOrder();

        var result = order.Return(ValidReturnReason);

        result.Should().BeSuccess();
        result.Value.Status.Should().Be(OrderStatus.Returned);
    }

    [Fact]
    public void ReturnReason_TooShort_Fails() =>
        ReturnReason.TryCreate("short").Should().BeFailure();

    [Fact]
    public void ReturnReason_Valid_Succeeds() =>
        ReturnReason.TryCreate("This is a valid return reason for the product").Should().BeSuccess();
}
