#pragma warning disable TRLS001, TRLS003

namespace Domain.Tests;

using OrderManagement.Domain;
using Trellis.Primitives;
using Trellis.Testing;

public class OverdueOrderSpecificationTests
{
    private static Order CreateSubmittedOrder(DateTime submittedAt)
    {
        var order = Order.TryCreate(CustomerId.NewUniqueV7(), "actor-1",
            [LineItem.Create(ProductId.NewUniqueV7(), ProductName.Create("Widget"), LineItemQuantity.Create(1), Money.Create(10m, "USD"))]).Value;
        order.Submit(submittedAt);
        return order;
    }

    [Fact]
    public void OverdueOrder_SubmittedMoreThan7DaysAgo_Matches()
    {
        var now = DateTime.UtcNow;
        var order = CreateSubmittedOrder(now.AddDays(-8));
        var spec = new OverdueOrderSpecification(now);

        spec.IsSatisfiedBy(order).Should().BeTrue();
    }

    [Fact]
    public void RecentOrder_SubmittedRecently_DoesNotMatch()
    {
        var now = DateTime.UtcNow;
        var order = CreateSubmittedOrder(now.AddDays(-3));
        var spec = new OverdueOrderSpecification(now);

        spec.IsSatisfiedBy(order).Should().BeFalse();
    }

    [Fact]
    public void ApprovedOrder_DoesNotMatch()
    {
        var now = DateTime.UtcNow;
        var order = CreateSubmittedOrder(now.AddDays(-10));
        order.Approve();
        var spec = new OverdueOrderSpecification(now);

        spec.IsSatisfiedBy(order).Should().BeFalse();
    }
}
