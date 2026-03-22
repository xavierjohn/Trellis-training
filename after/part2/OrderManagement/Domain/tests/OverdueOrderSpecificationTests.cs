namespace Domain.Tests;

using OrderManagement.Domain.Specifications;

public class OverdueOrderSpecificationTests
{
    [Fact]
    public void IsSatisfiedBy_SubmittedOrderOlderThan7Days_ReturnsTrue()
    {
        var spec = new OverdueOrderSpecification();
        var product = CreateTestProduct();
        _ = product.AddStock(100);
        var customerId = CustomerId.NewUniqueV4();
        var price = Money.Create(10m, "USD");
        Order.TryCreate(customerId, "actor", [(product.Id, "Product", 1, price)]).TryGetValue(out var order);
        _ = order!.Submit([product]);

        // A just-submitted order is not overdue (SubmittedAt is now)
        var satisfied = spec.IsSatisfiedBy(order);
        satisfied.Should().BeFalse();
    }

    [Fact]
    public void IsSatisfiedBy_DraftOrder_ReturnsFalse()
    {
        var spec = new OverdueOrderSpecification();
        var product = CreateTestProduct();
        var customerId = CustomerId.NewUniqueV4();
        var price = Money.Create(10m, "USD");
        Order.TryCreate(customerId, "actor", [(product.Id, "Product", 1, price)]).TryGetValue(out var order);

        var satisfied = spec.IsSatisfiedBy(order!);

        satisfied.Should().BeFalse();
    }

    private static Product CreateTestProduct()
    {
        var name = ProductName.Create("Test Product");
        var price = Money.Create(10m, "USD");
        Sku.TryCreate("TESTPROD1").TryGetValue(out var sku);
        Product.TryCreate(name, sku!, price).TryGetValue(out var product);
        return product!;
    }
}
