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

    [Fact]
    public void DeliveredAt_AfterDeliver_HasValue()
    {
        var (order, products) = CreateOrderWithProducts();
        _ = order.Submit(products);
        _ = order.Approve();
        _ = order.Ship();

        _ = order.Deliver();

        order.DeliveredAt.Should().HaveValue();
    }

    [Fact]
    public void Return_DeliveredWithinWindow_TransitionsToReturned()
    {
        var (order, products) = CreateOrderWithProducts();
        _ = order.Submit(products);
        _ = order.Approve();
        _ = order.Ship();
        _ = order.Deliver();

        ReturnReason.TryCreate("Damaged item received from seller").TryGetValue(out var reason);
        var result = order.Return(reason!, products);

        result.Should().BeSuccess()
            .Which.Status.Should().Be(OrderStatus.Returned);
    }

    [Fact]
    public void Return_AfterDelivered_SetsReturnedAt()
    {
        var (order, products) = CreateOrderWithProducts();
        _ = order.Submit(products);
        _ = order.Approve();
        _ = order.Ship();
        _ = order.Deliver();

        ReturnReason.TryCreate("Item does not match description at all").TryGetValue(out var reason);
        _ = order.Return(reason!, products);

        order.ReturnedAt.Should().HaveValue();
    }

    [Fact]
    public void Return_StockReleasedForEachLineItem()
    {
        var product = CreateTestProduct("RETSTOCK1");
        _ = product.AddStock(100);
        var price = Money.Create(10m, "USD");
        var customerId = CustomerId.NewUniqueV4();
        Order.TryCreate(customerId, "actor-1", [(product.Id, "Test Product", 5, price)]).TryGetValue(out var order);

        _ = order!.Submit([product]);
        _ = order.Approve();
        _ = order.Ship();
        _ = order.Deliver();

        var stockBeforeReturn = product.StockQuantity;
        ReturnReason.TryCreate("Changed my mind about this product").TryGetValue(out var reason);
        _ = order.Return(reason!, [product]);

        product.StockQuantity.Should().Be(stockBeforeReturn + 5);
    }

    [Fact]
    public void Return_FromShippedStatus_ReturnsConflictError()
    {
        var (order, products) = CreateOrderWithProducts();
        _ = order.Submit(products);
        _ = order.Approve();
        _ = order.Ship();

        ReturnReason.TryCreate("Wrong item shipped to my address").TryGetValue(out var reason);
        var result = order.Return(reason!, products);

        result.Should().BeFailure()
            .Which.Should().BeOfType<ConflictError>();
    }

    [Fact]
    public void Return_FromCancelledStatus_ReturnsConflictError()
    {
        var (order, _) = CreateOrderWithProducts();
        _ = order.Cancel();

        ReturnReason.TryCreate("This is a valid return reason text").TryGetValue(out var reason);
        var result = order.Return(reason!, []);

        result.Should().BeFailure()
            .Which.Should().BeOfType<ConflictError>();
    }

    [Fact]
    public void Return_AlreadyReturned_ReturnsConflictError()
    {
        var (order, products) = CreateOrderWithProducts();
        _ = order.Submit(products);
        _ = order.Approve();
        _ = order.Ship();
        _ = order.Deliver();

        ReturnReason.TryCreate("First return reason for this order here").TryGetValue(out var reason);
        _ = order.Return(reason!, products);

        ReturnReason.TryCreate("Second return attempt should fail here").TryGetValue(out var reason2);
        var result = order.Return(reason2!, products);

        result.Should().BeFailure()
            .Which.Should().BeOfType<ConflictError>();
    }

    [Fact]
    public void Return_After30Days_ReturnsDomainError()
    {
        var (order, products) = CreateOrderWithProducts();
        _ = order.Submit(products);
        _ = order.Approve();
        _ = order.Ship();
        _ = order.Deliver();

        // Simulate delivery more than 30 days ago
        order.DeliveredAt = Maybe.From(DateTime.UtcNow.AddDays(-31));

        ReturnReason.TryCreate("Item is no longer needed by customer").TryGetValue(out var reason);
        var result = order.Return(reason!, products);

        result.Should().BeFailure()
            .Which.Should().BeOfType<DomainError>();
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
