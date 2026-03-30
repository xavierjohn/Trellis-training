namespace Domain.Tests;

using OrderManagement.Domain;
using Trellis.Primitives;

#pragma warning disable TRLS003
#pragma warning disable TRLS001

public class OrderTests
{
    private static CustomerId TestCustomerId => CustomerId.NewUniqueV7();

    private static (Product Product, LineItem LineItem) CreateProductAndLineItem(int quantity = 1, decimal price = 10.00m, int stock = 100)
    {
        var product = Product.TryCreate(
            ProductName.Create("Test Product"),
            Sku.Create($"TST-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}"),
            Money.Create(price, "USD")).Value;
        if (stock > 0)
            product.AddStock(StockQuantity.Create(stock));

        var lineItem = new LineItem(product.Id, product.ProductName, LineItemQuantity.Create(quantity), product.UnitPrice);
        return (product, lineItem);
    }

    private static (Order Order, List<Product> Products) CreateDraftOrderWithProducts(int lineItemCount = 1)
    {
        var products = new List<Product>();
        var lineItems = new List<LineItem>();
        for (var i = 0; i < lineItemCount; i++)
        {
            var (product, lineItem) = CreateProductAndLineItem();
            products.Add(product);
            lineItems.Add(lineItem);
        }

        var order = Order.TryCreate(TestCustomerId, "actor-1", lineItems).Value;
        return (order, products);
    }

    [Fact]
    public void TryCreate_valid_order_succeeds()
    {
        var (_, li) = CreateProductAndLineItem();
        var result = Order.TryCreate(TestCustomerId, "actor-1", [li]);

        result.Should().BeSuccess();
        var order = result.Value;
        order.Status.Should().Be(OrderStatus.Draft);
        order.LineItems.Should().HaveCount(1);
        order.SubmittedAt.Should().BeNone();
        order.ShippedAt.Should().BeNone();
    }

    [Fact]
    public void TryCreate_empty_line_items_fails()
    {
        var result = Order.TryCreate(TestCustomerId, "actor-1", []);

        result.Should().BeFailure()
            .Which.Should().BeOfType<ValidationError>();
    }

    [Fact]
    public void TryCreate_duplicate_products_fails()
    {
        var (product, li1) = CreateProductAndLineItem();
        var li2 = new LineItem(product.Id, product.ProductName, LineItemQuantity.Create(2), product.UnitPrice);

        var result = Order.TryCreate(TestCustomerId, "actor-1", [li1, li2]);

        result.Should().BeFailure();
    }

    [Fact]
    public void AddLineItem_to_draft_succeeds()
    {
        var (order, _) = CreateDraftOrderWithProducts();
        var (_, newItem) = CreateProductAndLineItem();

        var result = order.AddLineItem(newItem);

        result.Should().BeSuccess();
        order.LineItems.Should().HaveCount(2);
    }

    [Fact]
    public void AddLineItem_duplicate_product_fails()
    {
        var (product, li) = CreateProductAndLineItem();
        var order = Order.TryCreate(TestCustomerId, "actor-1", [li]).Value;
        var duplicate = new LineItem(product.Id, product.ProductName, LineItemQuantity.Create(2), product.UnitPrice);

        var result = order.AddLineItem(duplicate);

        result.Should().BeFailure();
    }

    [Fact]
    public void RemoveLineItem_succeeds()
    {
        var (order, _) = CreateDraftOrderWithProducts(2);

        var result = order.RemoveLineItem(order.LineItems[0].Id);

        result.Should().BeSuccess();
        order.LineItems.Should().HaveCount(1);
    }

    [Fact]
    public void RemoveLineItem_last_item_fails()
    {
        var (order, _) = CreateDraftOrderWithProducts(1);

        var result = order.RemoveLineItem(order.LineItems[0].Id);

        result.Should().BeFailure();
    }

    [Fact]
    public void Submit_reserves_stock_and_transitions()
    {
        var (order, products) = CreateDraftOrderWithProducts();

        var result = order.Submit(products);

        result.Should().BeSuccess();
        order.Status.Should().Be(OrderStatus.Submitted);
        order.SubmittedAt.Should().HaveValue();
    }

    [Fact]
    public void Approve_from_submitted_succeeds()
    {
        var (order, products) = CreateDraftOrderWithProducts();
        order.Submit(products);

        var result = order.Approve();

        result.Should().BeSuccess();
        order.Status.Should().Be(OrderStatus.Approved);
    }

    [Fact]
    public void Ship_from_approved_succeeds()
    {
        var (order, products) = CreateDraftOrderWithProducts();
        order.Submit(products);
        order.Approve();

        var result = order.Ship();

        result.Should().BeSuccess();
        order.Status.Should().Be(OrderStatus.Shipped);
        order.ShippedAt.Should().HaveValue();
    }

    [Fact]
    public void Deliver_from_shipped_succeeds()
    {
        var (order, products) = CreateDraftOrderWithProducts();
        order.Submit(products);
        order.Approve();
        order.Ship();

        var result = order.Deliver();

        result.Should().BeSuccess();
        order.Status.Should().Be(OrderStatus.Delivered);
    }

    [Fact]
    public void Cancel_from_draft_succeeds()
    {
        var (order, _) = CreateDraftOrderWithProducts();

        var result = order.Cancel();

        result.Should().BeSuccess();
        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void Cancel_from_submitted_releases_stock()
    {
        var (product, li) = CreateProductAndLineItem(3, 10.00m, 100);
        var order = Order.TryCreate(TestCustomerId, "actor-1", [li]).Value;
        order.Submit([product]);
        var stockAfterSubmit = product.StockQuantity.Value;

        order.Cancel([product]);

        product.StockQuantity.Value.Should().Be(stockAfterSubmit + 3);
        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void Cancel_from_shipped_fails()
    {
        var (order, products) = CreateDraftOrderWithProducts();
        order.Submit(products);
        order.Approve();
        order.Ship();

        var result = order.Cancel();

        result.Should().BeFailure();
    }

    [Fact]
    public void Cancel_from_delivered_fails()
    {
        var (order, products) = CreateDraftOrderWithProducts();
        order.Submit(products);
        order.Approve();
        order.Ship();
        order.Deliver();

        var result = order.Cancel();

        result.Should().BeFailure();
    }

    [Fact]
    public void Draft_to_approved_fails()
    {
        var (order, _) = CreateDraftOrderWithProducts();

        var result = order.Approve();

        result.Should().BeFailure();
    }

    [Fact]
    public void Submit_raises_OrderSubmittedEvent()
    {
        var (order, products) = CreateDraftOrderWithProducts();
        order.AcceptChanges();

        order.Submit(products);

        order.UncommittedEvents().Should().ContainSingle()
            .Which.Should().BeOfType<OrderSubmittedEvent>();
    }
}
