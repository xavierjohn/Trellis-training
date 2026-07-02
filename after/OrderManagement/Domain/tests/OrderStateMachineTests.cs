namespace Domain.Tests;

using Microsoft.Extensions.Time.Testing;
using OrderManagement.Domain;
using Trellis.Authorization;

public class OrderStateMachineTests
{
    private static (Order order, Product product, Customer customer, FakeTimeProvider clock) Arrange()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 12, 8, 0, 0, TimeSpan.Zero));

        var customer = new Customer(
            FirstName.Create("Ada"),
            LastName.Create("Lovelace"),
            Trellis.Primitives.EmailAddress.Create("ada@example.com"),
            Maybe<Trellis.Primitives.PhoneNumber>.None,
            new ShippingAddress(
                Street.Create("1 Compute Way"),
                City.Create("Mountain View"),
                StateRegion.Create("CA"),
                PostalCode.Create("94043"),
                Country.Create("USA")));

        var product = new Product(
            ProductName.Create("Widget"),
            Sku.Create("WIDGET01"),
            UnitPrice.Create(9.99m));
        product.AddStock(50).IsSuccess.Should().BeTrue();

        var actorId = ActorId.Create("actor-1");
        var order = new Order(customer.Id, actorId, clock);
        order.AddLineItem(product.Id, product.ProductName, LineItemQuantity.Create(3), product.UnitPrice)
            .IsSuccess.Should().BeTrue();

        return (order, product, customer, clock);
    }

    [Fact]
    public void Submit_FromDraft_TransitionsAndReservesStock()
    {
        var (order, product, _, clock) = Arrange();
        var initialStock = product.StockQuantity.Value;

        var result = order.Submit(new Dictionary<ProductId, Product> { [product.Id] = product }, clock);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Submitted);
        order.SubmittedAt.HasValue.Should().BeTrue();
        product.StockQuantity.Value.Should().Be(initialStock - 3);
        order.UncommittedEvents().OfType<OrderSubmittedEvent>().Should().HaveCount(1);
    }

    [Fact]
    public void Submit_WithInsufficientStock_FailsAndDoesNotReserveAnyStock()
    {
        var (order, product, _, clock) = Arrange();
        // Drain stock so the reservation fails.
        product.ReserveStock(48).IsSuccess.Should().BeTrue();
        var stockBefore = product.StockQuantity.Value;

        var result = order.Submit(new Dictionary<ProductId, Product> { [product.Id] = product }, clock);

        result.IsFailure.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Draft);
        order.SubmittedAt.HasValue.Should().BeFalse();
        product.StockQuantity.Value.Should().Be(stockBefore, "no partial reservation may leak through");
    }

    [Fact]
    public void StateMachine_RejectsInvalidTransitions()
    {
        var (order, _, _, clock) = Arrange();

        // Ship before the order has been submitted/approved is an invalid state-machine
        // transition (Draft only permits Submit or Cancel).
        var result = order.Ship(clock);

        result.IsFailure.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Draft);
    }

    [Fact]
    public void Cancel_FromSubmitted_ReleasesReservedStock()
    {
        var (order, product, _, clock) = Arrange();
        var products = new Dictionary<ProductId, Product> { [product.Id] = product };
        order.Submit(products, clock).IsSuccess.Should().BeTrue();
        var stockAfterSubmit = product.StockQuantity.Value;

        var result = order.Cancel(products, clock);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
        product.StockQuantity.Value.Should().Be(stockAfterSubmit + 3, "cancelling a submitted order returns the reserved stock");
    }

    [Fact]
    public void Cancel_FromDraft_DoesNotTouchStock()
    {
        var (order, product, _, clock) = Arrange();
        var stockBefore = product.StockQuantity.Value;

        var result = order.Cancel(new Dictionary<ProductId, Product> { [product.Id] = product }, clock);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
        product.StockQuantity.Value.Should().Be(stockBefore, "draft orders never reserve stock so cancelling cannot release it");
    }

    [Fact]
    public void FullLifecycle_DraftToDelivered_FiresEveryEvent()
    {
        var (order, product, _, clock) = Arrange();
        var products = new Dictionary<ProductId, Product> { [product.Id] = product };

        order.Submit(products, clock).IsSuccess.Should().BeTrue();
        order.RecordPayment(PaymentRef.Create($"PAY-{order.Id.Value:N}"), order.OrderTotal, clock.GetUtcNow())
            .IsSuccess.Should().BeTrue();
        order.Approve(clock).IsSuccess.Should().BeTrue();
        order.Ship(clock).IsSuccess.Should().BeTrue();
        order.Deliver(clock).IsSuccess.Should().BeTrue();

        order.Status.Should().Be(OrderStatus.Delivered);
        order.UncommittedEvents().Should().HaveCount(5);
        order.UncommittedEvents().Select(e => e.GetType()).Should().Equal(
            typeof(OrderSubmittedEvent),
            typeof(OrderPaidEvent),
            typeof(OrderApprovedEvent),
            typeof(OrderShippedEvent),
            typeof(OrderDeliveredEvent));
    }

    [Fact]
    public void Approve_BeforePaymentConfirmed_Fails()
    {
        var (order, product, _, clock) = Arrange();
        order.Submit(new Dictionary<ProductId, Product> { [product.Id] = product }, clock)
            .IsSuccess.Should().BeTrue();

        // Approval is gated on the payment round-trip: an unpaid Submitted order cannot be approved.
        var result = order.Approve(clock);

        result.IsFailure.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Submitted);
    }

    [Fact]
    public void RecordPayment_ThenApprove_Succeeds()
    {
        var (order, product, _, clock) = Arrange();
        order.Submit(new Dictionary<ProductId, Product> { [product.Id] = product }, clock)
            .IsSuccess.Should().BeTrue();

        order.RecordPayment(PaymentRef.Create("PAY-001"), order.OrderTotal, clock.GetUtcNow())
            .IsSuccess.Should().BeTrue();

        order.PaidAt.HasValue.Should().BeTrue();
        order.UncommittedEvents().OfType<OrderPaidEvent>().Should().HaveCount(1);
        order.Approve(clock).IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Approved);
    }

    [Fact]
    public void RecordPayment_ExactDuplicate_IsIdempotent()
    {
        var (order, product, _, clock) = Arrange();
        order.Submit(new Dictionary<ProductId, Product> { [product.Id] = product }, clock)
            .IsSuccess.Should().BeTrue();
        var paymentRef = PaymentRef.Create("PAY-001");

        order.RecordPayment(paymentRef, order.OrderTotal, clock.GetUtcNow()).IsSuccess.Should().BeTrue();
        var second = order.RecordPayment(paymentRef, order.OrderTotal, clock.GetUtcNow());

        second.IsSuccess.Should().BeTrue();
        order.UncommittedEvents().OfType<OrderPaidEvent>().Should().HaveCount(1, "an exact duplicate payment is a no-op");
    }

    [Fact]
    public void RecordPayment_DifferentPayment_Conflicts()
    {
        var (order, product, _, clock) = Arrange();
        order.Submit(new Dictionary<ProductId, Product> { [product.Id] = product }, clock)
            .IsSuccess.Should().BeTrue();
        order.RecordPayment(PaymentRef.Create("PAY-001"), order.OrderTotal, clock.GetUtcNow())
            .IsSuccess.Should().BeTrue();

        var conflict = order.RecordPayment(PaymentRef.Create("PAY-002"), order.OrderTotal, clock.GetUtcNow());

        conflict.IsFailure.Should().BeTrue();
        conflict.Error.Should().BeOfType<Error.Conflict>();
    }
}