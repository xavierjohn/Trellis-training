namespace Domain.Tests;

using Microsoft.Extensions.Time.Testing;
using OrderManagement.Domain;
using Trellis.Authorization;

public class OverdueOrderSpecificationTests
{
    [Fact]
    public void DraftOrders_AreNeverOverdue()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var order = CreateOrder(clock);
        var spec = new OverdueOrderSpecification(clock.GetUtcNow().AddDays(30));

        spec.IsSatisfiedBy(order).Should().BeFalse();
    }

    [Fact]
    public void SubmittedOrder_LessThanSevenDays_IsNotOverdue()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero));
        var order = CreateOrder(clock);
        SubmitFully(order, clock);

        var spec = new OverdueOrderSpecification(clock.GetUtcNow().AddDays(6));
        spec.IsSatisfiedBy(order).Should().BeFalse();
    }

    [Fact]
    public void SubmittedOrder_MoreThanSevenDays_IsOverdue()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero));
        var order = CreateOrder(clock);
        SubmitFully(order, clock);

        var spec = new OverdueOrderSpecification(clock.GetUtcNow().AddDays(8));
        spec.IsSatisfiedBy(order).Should().BeTrue();
    }

    [Fact]
    public void ApprovedOrder_IsNotOverdue_RegardlessOfAge()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero));
        var order = CreateOrder(clock);
        SubmitFully(order, clock);
        order.RecordPayment(PaymentRef.Create($"PAY-{order.Id.Value:N}"), order.OrderTotal, clock.GetUtcNow())
            .IsSuccess.Should().BeTrue();
        order.Approve(clock).IsSuccess.Should().BeTrue();

        var spec = new OverdueOrderSpecification(clock.GetUtcNow().AddDays(365));
        spec.IsSatisfiedBy(order).Should().BeFalse();
    }

    private static Order CreateOrder(TimeProvider clock)
    {
        var product = new Product(
            ProductName.Create("Widget"),
            Sku.Create("WIDGET01"),
            UnitPrice.Create(1m));
        product.AddStock(100).IsSuccess.Should().BeTrue();

        var customerId = CustomerId.NewUniqueV7();
        var order = new Order(customerId, ActorId.Create("actor-1"), clock);
        order.AddLineItem(product.Id, product.ProductName, LineItemQuantity.Create(1), product.UnitPrice)
            .IsSuccess.Should().BeTrue();
        OrderProducts[order.Id] = product;
        return order;
    }

    private static readonly Dictionary<OrderId, Product> OrderProducts = new();

    private static void SubmitFully(Order order, TimeProvider clock)
    {
        var product = OrderProducts[order.Id];
        order.Submit(new Dictionary<ProductId, Product> { [product.Id] = product }, clock)
            .IsSuccess.Should().BeTrue();
    }
}