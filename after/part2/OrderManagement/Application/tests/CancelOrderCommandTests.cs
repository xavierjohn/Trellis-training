namespace Application.Tests;

using Mediator;
using OrderManagement.Application.Orders;
using OrderManagement.Domain;
using Trellis.Authorization;
using Trellis.Primitives;
using Trellis.Testing.Fakes;

#pragma warning disable TRLS003 // Tests assert success before accessing .Value

public class CancelOrderCommandTests
{
    private readonly ISender _sender;
    private readonly FakeRepository<Order, OrderId> _orderRepo;
    private readonly FakeRepository<Product, ProductId> _productRepo;
    private readonly TestActorProvider _actorProvider;

    public CancelOrderCommandTests(
        ISender sender,
        FakeRepository<Order, OrderId> orderRepo,
        FakeRepository<Product, ProductId> productRepo,
        TestActorProvider actorProvider)
    {
        _sender = sender;
        _orderRepo = orderRepo;
        _productRepo = productRepo;
        _actorProvider = actorProvider;
    }

    private static OrderLineItemInput MakeLineItem() =>
        new(ProductId.NewUniqueV7(), ProductName.Create("Widget"), LineItemQuantity.Create(1), Money.Create(9.99m, "USD"));

    private async Task<Order> SaveDraftOrderAs(string actorId)
    {
        var order = Order.TryCreate(CustomerId.NewUniqueV7(), actorId, [MakeLineItem()]).Value;
        (await _orderRepo.SaveAsync(order, TestContext.Current.CancellationToken)).Should().BeSuccess();
        return order;
    }

    [Fact]
    public async Task Cancel_order_as_owner_succeeds()
    {
        await using var _ = _actorProvider.WithActor("owner-1", Permissions.OrdersCancel);
        var order = await SaveDraftOrderAs("owner-1");

        var result = await _sender.Send(new CancelOrderCommand(order.Id), TestContext.Current.CancellationToken);

        result.Should().BeSuccess();
        result.Value.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task Cancel_order_by_non_owner_returns_forbidden()
    {
        var order = await SaveDraftOrderAs("owner-1");
        await using var _ = _actorProvider.WithActor("other-user", Permissions.OrdersCancel);

        var result = await _sender.Send(new CancelOrderCommand(order.Id), TestContext.Current.CancellationToken);

        result.Should().BeFailureOfType<ForbiddenError>();
    }

    [Fact]
    public async Task Cancel_order_by_admin_with_read_all_permission_succeeds()
    {
        var order = await SaveDraftOrderAs("owner-1");
        await using var _ = _actorProvider.WithActor("admin", Permissions.OrdersCancel, Permissions.OrdersReadAll);

        var result = await _sender.Send(new CancelOrderCommand(order.Id), TestContext.Current.CancellationToken);

        result.Should().BeSuccess();
        result.Value.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task Cancel_nonexistent_order_returns_not_found()
    {
        await using var _ = _actorProvider.WithActor("test-user", Permissions.OrdersCancel, Permissions.OrdersReadAll);
        var fakeId = OrderId.NewUniqueV7();

        var result = await _sender.Send(new CancelOrderCommand(fakeId), TestContext.Current.CancellationToken);

        result.Should().BeFailureOfType<NotFoundError>();
    }
}
