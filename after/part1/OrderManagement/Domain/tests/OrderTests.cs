namespace Domain.Tests;

public class OrderTests
{
    [Fact]
    public void TryCreate_WithValidData_CreatesDraftOrder()
    {
        var customerId = CustomerId.NewUniqueV4();
        var product = CreateTestProduct("SKU001");
        _ = product.AddStock(100);
        var price = Money.Create(10m, "USD");

        var result = Order.TryCreate(
            customerId,
            "actor-1",
            [(product.Id, "Test Product", 5, price)]);

        result.Should().BeSuccess()
            .Which.Status.Should().Be(OrderStatus.Draft);
    }

    [Fact]
    public void Submit_WithStock_TransitionsToSubmitted()
    {
        var (order, products) = CreateOrderWithProducts();

        var result = order.Submit(products);

        result.Should().BeSuccess()
            .Which.Status.Should().Be(OrderStatus.Submitted);
    }

    [Fact]
    public void Submit_AlreadySubmitted_ReturnsConflictError()
    {
        var (order, products) = CreateOrderWithProducts();
        _ = order.Submit(products);

        var result = order.Submit(products);

        result.Should().BeFailure()
            .Which.Should().BeOfType<ConflictError>();
    }

    [Fact]
    public void Submit_InsufficientStock_ReturnsDomainError()
    {
        var product = CreateTestProduct("LOWSTOCK1");
        _ = product.AddStock(1);
        var price = Money.Create(10m, "USD");
        var customerId = CustomerId.NewUniqueV4();
        Order.TryCreate(customerId, "actor-1", [(product.Id, "Low Stock", 5, price)]).TryGetValue(out var order);

        var result = order!.Submit([product]);

        result.Should().BeFailure()
            .Which.Should().BeOfType<DomainError>();
    }

    [Fact]
    public void Approve_AfterSubmit_TransitionsToApproved()
    {
        var (order, products) = CreateOrderWithProducts();
        _ = order.Submit(products);

        var result = order.Approve();

        result.Should().BeSuccess()
            .Which.Status.Should().Be(OrderStatus.Approved);
    }

    [Fact]
    public void Ship_AfterApprove_TransitionsToShipped()
    {
        var (order, products) = CreateOrderWithProducts();
        _ = order.Submit(products);
        _ = order.Approve();

        var result = order.Ship();

        result.Should().BeSuccess()
            .Which.Status.Should().Be(OrderStatus.Shipped);
    }

    [Fact]
    public void Deliver_AfterShip_TransitionsToDelivered()
    {
        var (order, products) = CreateOrderWithProducts();
        _ = order.Submit(products);
        _ = order.Approve();
        _ = order.Ship();

        var result = order.Deliver();

        result.Should().BeSuccess()
            .Which.Status.Should().Be(OrderStatus.Delivered);
    }

    [Fact]
    public void Cancel_DraftOrder_TransitionsToCancelled()
    {
        var (order, _) = CreateOrderWithProducts();

        var result = order.Cancel();

        result.Should().BeSuccess()
            .Which.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void Cancel_Delivered_ReturnsConflictError()
    {
        var (order, products) = CreateOrderWithProducts();
        _ = order.Submit(products);
        _ = order.Approve();
        _ = order.Ship();
        _ = order.Deliver();

        var result = order.Cancel();

        result.Should().BeFailure()
            .Which.Should().BeOfType<ConflictError>();
    }

    [Fact]
    public void SubmittedAt_AfterSubmit_HasValue()
    {
        var (order, products) = CreateOrderWithProducts();

        _ = order.Submit(products);

        order.SubmittedAt.Should().HaveValue();
    }

    private static (Order order, List<Product> products) CreateOrderWithProducts()
    {
        var product = CreateTestProduct("TESTPROD1");
        _ = product.AddStock(100);
        var price = Money.Create(10m, "USD");
        var customerId = CustomerId.NewUniqueV4();

        Order.TryCreate(customerId, "actor-1", [(product.Id, "Test Product", 5, price)]).TryGetValue(out var order);

        return (order!, [product]);
    }

    private static Product CreateTestProduct(string sku)
    {
        var name = ProductName.Create("Test Product");
        var price = Money.Create(10m, "USD");
        Sku.TryCreate(sku).TryGetValue(out var skuVal);
        Product.TryCreate(name, skuVal!, price).TryGetValue(out var product);
        return product!;
    }
}
