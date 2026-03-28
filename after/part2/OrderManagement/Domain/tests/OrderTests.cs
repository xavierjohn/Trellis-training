namespace Domain.Tests;

using OrderManagement.Domain;
using Trellis.Primitives;

#pragma warning disable TRLS003 // Tests assert success before accessing .Value

public class OrderTests
{
    private static CustomerId TestCustomerId => CustomerId.NewUniqueV7();

    private static OrderLineItemInput MakeLineItem(ProductId? productId = null) =>
        new(productId ?? ProductId.NewUniqueV7(),
            ProductName.Create("Widget"),
            LineItemQuantity.Create(1),
            Money.Create(9.99m, "USD"));

    private static Order CreateDraftOrder(string actorId = "actor-1") =>
        Order.TryCreate(TestCustomerId, actorId, [MakeLineItem()]).Value;

    [Fact]
    public void TryCreate_with_one_line_item_creates_draft_order()
    {
        var result = Order.TryCreate(TestCustomerId, "actor-1", [MakeLineItem()]);

        result.Should().BeSuccess();
        var order = result.Value;
        order.Status.Should().Be(OrderStatus.Draft);
        order.LineItems.Should().HaveCount(1);
        order.CreatedByActorId.Should().Be("actor-1");
    }

    [Fact]
    public void TryCreate_with_multiple_distinct_products_succeeds()
    {
        var items = new[] { MakeLineItem(), MakeLineItem() };

        var result = Order.TryCreate(TestCustomerId, "actor-1", items);

        result.Should().BeSuccess();
        result.Value.LineItems.Should().HaveCount(2);
    }

    [Fact]
    public void TryCreate_with_no_line_items_fails()
    {
        var result = Order.TryCreate(TestCustomerId, "actor-1", []);

        result.Should().BeFailure();
    }

    [Fact]
    public void TryCreate_with_duplicate_products_fails()
    {
        var productId = ProductId.NewUniqueV7();

        var result = Order.TryCreate(TestCustomerId, "actor-1", [MakeLineItem(productId), MakeLineItem(productId)]);

        result.Should().BeFailure();
    }

    [Fact]
    public void AddLineItem_to_draft_order_succeeds()
    {
        var order = CreateDraftOrder();
        var newProductId = ProductId.NewUniqueV7();

        var result = order.AddLineItem(newProductId, ProductName.Create("Gadget"), LineItemQuantity.Create(2), Money.Create(5.00m, "USD"));

        result.Should().BeSuccess();
        order.LineItems.Should().HaveCount(2);
    }

    [Fact]
    public void AddLineItem_duplicate_product_fails()
    {
        var productId = ProductId.NewUniqueV7();
        var order = Order.TryCreate(TestCustomerId, "actor-1", [MakeLineItem(productId)]).Value;

        var result = order.AddLineItem(productId, ProductName.Create("Widget"), LineItemQuantity.Create(1), Money.Create(9.99m, "USD"));

        result.Should().BeFailure();
    }

    [Fact]
    public void RemoveLineItem_from_order_with_multiple_items_succeeds()
    {
        var order = Order.TryCreate(TestCustomerId, "actor-1", [MakeLineItem(), MakeLineItem()]).Value;
        var lineItemId = order.LineItems[0].Id;

        var result = order.RemoveLineItem(lineItemId);

        result.Should().BeSuccess();
        order.LineItems.Should().HaveCount(1);
    }

    [Fact]
    public void RemoveLineItem_last_item_fails()
    {
        var order = CreateDraftOrder();
        var lineItemId = order.LineItems[0].Id;

        var result = order.RemoveLineItem(lineItemId);

        result.Should().BeFailure();
    }

    // --- State machine: valid transitions ---

    [Fact]
    public void Submit_from_Draft_transitions_to_Submitted_and_sets_SubmittedAt()
    {
        var order = CreateDraftOrder();

        var result = order.Submit();

        result.Should().BeSuccess();
        order.Status.Should().Be(OrderStatus.Submitted);
        order.SubmittedAt.Should().HaveValue();
    }

    [Fact]
    public void Approve_from_Submitted_transitions_to_Approved()
    {
        var order = CreateDraftOrder();
        order.Submit().Should().BeSuccess();

        var result = order.Approve();

        result.Should().BeSuccess();
        order.Status.Should().Be(OrderStatus.Approved);
    }

    [Fact]
    public void Ship_from_Approved_transitions_to_Shipped_and_sets_ShippedAt()
    {
        var order = CreateDraftOrder();
        order.Submit().Should().BeSuccess();
        order.Approve().Should().BeSuccess();

        var result = order.Ship();

        result.Should().BeSuccess();
        order.Status.Should().Be(OrderStatus.Shipped);
        order.ShippedAt.Should().HaveValue();
    }

    [Fact]
    public void Deliver_from_Shipped_transitions_to_Delivered()
    {
        var order = CreateDraftOrder();
        order.Submit().Should().BeSuccess();
        order.Approve().Should().BeSuccess();
        order.Ship().Should().BeSuccess();

        var result = order.Deliver();

        result.Should().BeSuccess();
        order.Status.Should().Be(OrderStatus.Delivered);
    }

    [Fact]
    public void Cancel_from_Draft_transitions_to_Cancelled()
    {
        var order = CreateDraftOrder();

        var result = order.Cancel();

        result.Should().BeSuccess();
        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void Cancel_from_Submitted_transitions_to_Cancelled()
    {
        var order = CreateDraftOrder();
        order.Submit().Should().BeSuccess();

        var result = order.Cancel();

        result.Should().BeSuccess();
        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void Cancel_from_Approved_transitions_to_Cancelled()
    {
        var order = CreateDraftOrder();
        order.Submit().Should().BeSuccess();
        order.Approve().Should().BeSuccess();

        var result = order.Cancel();

        result.Should().BeSuccess();
        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    // --- State machine: invalid transitions ---

    [Fact]
    public void Invalid_transition_Deliver_from_Draft_fails()
    {
        var order = CreateDraftOrder();

        var result = order.Deliver();

        result.Should().BeFailure();
    }

    [Fact]
    public void Cannot_cancel_from_Delivered_status()
    {
        var order = CreateDraftOrder();
        order.Submit().Should().BeSuccess();
        order.Approve().Should().BeSuccess();
        order.Ship().Should().BeSuccess();
        order.Deliver().Should().BeSuccess();

        var result = order.Cancel();

        result.Should().BeFailure();
    }

    // --- Domain events ---

    [Fact]
    public void Submit_raises_OrderSubmittedEvent()
    {
        var order = CreateDraftOrder();
        order.AcceptChanges();

        order.Submit().Should().BeSuccess();

        order.UncommittedEvents().Should().ContainSingle()
            .Which.Should().BeOfType<OrderSubmittedEvent>();
    }

    // --- HadStockReserved ---

    [Fact]
    public void HadStockReserved_is_false_for_draft_order()
    {
        var order = CreateDraftOrder();

        order.HadStockReserved.Should().BeFalse();
    }

    [Fact]
    public void HadStockReserved_is_true_after_submit()
    {
        var order = CreateDraftOrder();
        order.Submit().Should().BeSuccess();

        order.HadStockReserved.Should().BeTrue();
    }

    [Fact]
    public void HadStockReserved_is_true_after_approve()
    {
        var order = CreateDraftOrder();
        order.Submit().Should().BeSuccess();
        order.Approve().Should().BeSuccess();

        order.HadStockReserved.Should().BeTrue();
    }
}
