namespace Domain.Tests;

using OrderManagement.Domain;
using Trellis.Primitives;

#pragma warning disable TRLS003 // Tests assert success before accessing .Value

public class OverdueOrderSpecificationTests
{
    private static Order CreateSubmittedOrder()
    {
        var lineItem = new OrderLineItemInput(
            ProductId.NewUniqueV7(),
            ProductName.Create("Widget"),
            LineItemQuantity.Create(1),
            Money.Create(9.99m, "USD"));

        var order = Order.TryCreate(CustomerId.NewUniqueV7(), "actor-1", [lineItem]).Value;
        order.Submit().Should().BeSuccess();
        return order;
    }

    [Fact]
    public void Matches_submitted_order_older_than_cutoff()
    {
        var order = CreateSubmittedOrder();
        // asOf is in the future relative to SubmittedAt (now), so SubmittedAt < asOf => overdue
        var spec = new OverdueOrderSpecification(DateTime.UtcNow.AddDays(8));

        spec.IsSatisfiedBy(order).Should().BeTrue();
    }

    [Fact]
    public void Does_not_match_submitted_order_newer_than_cutoff()
    {
        var order = CreateSubmittedOrder();
        // asOf is in the past relative to SubmittedAt (now), so SubmittedAt < asOf is false
        var spec = new OverdueOrderSpecification(DateTime.UtcNow.AddDays(-1));

        spec.IsSatisfiedBy(order).Should().BeFalse();
    }

    [Fact]
    public void Does_not_match_draft_order()
    {
        var lineItem = new OrderLineItemInput(
            ProductId.NewUniqueV7(),
            ProductName.Create("Widget"),
            LineItemQuantity.Create(1),
            Money.Create(9.99m, "USD"));
        var order = Order.TryCreate(CustomerId.NewUniqueV7(), "actor-1", [lineItem]).Value;

        var spec = new OverdueOrderSpecification(DateTime.UtcNow.AddDays(8));

        spec.IsSatisfiedBy(order).Should().BeFalse();
    }

    [Fact]
    public void Does_not_match_approved_order()
    {
        var order = CreateSubmittedOrder();
        order.Approve().Should().BeSuccess();

        var spec = new OverdueOrderSpecification(DateTime.UtcNow.AddDays(8));

        spec.IsSatisfiedBy(order).Should().BeFalse();
    }
}
