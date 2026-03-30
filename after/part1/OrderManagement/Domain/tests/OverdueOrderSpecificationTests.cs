namespace Domain.Tests;

using OrderManagement.Domain;
using Trellis.Primitives;

#pragma warning disable TRLS003
#pragma warning disable TRLS001

public class OverdueOrderSpecificationTests
{
    private static (Product Product, LineItem LineItem) CreateProductAndLineItem()
    {
        var product = Product.TryCreate(
            ProductName.Create("Test"),
            Sku.Create($"TST-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}"),
            Money.Create(10m, "USD")).Value;
        product.AddStock(StockQuantity.Create(100));
        var li = new LineItem(product.Id, product.ProductName, LineItemQuantity.Create(1), product.UnitPrice);
        return (product, li);
    }

    private static Order CreateSubmittedOrder()
    {
        var (product, li) = CreateProductAndLineItem();
        var order = Order.TryCreate(CustomerId.NewUniqueV7(), "actor-1", [li]).Value;
        order.Submit([product]);
        return order;
    }

    [Fact]
    public void Matches_submitted_order_older_than_7_days()
    {
        var order = CreateSubmittedOrder();

        // asOf = now + 8 days → cutoff = now + 1 day → SubmittedAt (~now) < cutoff ✓
        var futureDate = DateTime.UtcNow.AddDays(8);
        var spec = new OverdueOrderSpecification(futureDate);

        spec.IsSatisfiedBy(order).Should().BeTrue();
    }

    [Fact]
    public void Does_not_match_recently_submitted_order()
    {
        var order = CreateSubmittedOrder();

        // Same day — not overdue
        var spec = new OverdueOrderSpecification(DateTime.UtcNow);

        spec.IsSatisfiedBy(order).Should().BeFalse();
    }

    [Fact]
    public void Does_not_match_approved_order()
    {
        var (product, li) = CreateProductAndLineItem();
        var order = Order.TryCreate(CustomerId.NewUniqueV7(), "actor-1", [li]).Value;
        order.Submit([product]);
        order.Approve();

        var futureDate = DateTime.UtcNow.AddDays(8);
        var spec = new OverdueOrderSpecification(futureDate);

        spec.IsSatisfiedBy(order).Should().BeFalse();
    }
}
