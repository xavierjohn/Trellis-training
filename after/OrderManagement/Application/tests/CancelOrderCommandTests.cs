namespace Application.Tests;

using Microsoft.Extensions.Time.Testing;
using OrderManagement.Application.Orders;
using OrderManagement.Domain;
using Trellis.Authorization;
using Trellis.Testing;

/// <summary>
/// Direct unit tests for the <see cref="CancelOrderCommand"/>'s ownership-checked
/// <see cref="IAuthorizeResource{Order}.Authorize"/> implementation (spec §5.4).
/// Bypasses the mediator pipeline to isolate the authorization rule under test —
/// pipeline-integration coverage lives in the Api.Tests layer.
/// </summary>
public class CancelOrderCommandTests
{
    [Fact]
    public async Task Authorize_Allows_OwnerActor()
    {
        var order = ArrangeOrder("actor-owner");
        var owner = await NewActor("actor-owner", Permissions.OrdersCancel);
        var command = new CancelOrderCommand(order.Id);

        var result = command.Authorize(owner, order);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Authorize_Allows_AdminActor_WithReadAll_EvenIfNotOwner()
    {
        var order = ArrangeOrder("actor-owner");
        var admin = await NewActor("actor-admin", Permissions.OrdersCancel, Permissions.OrdersReadAll);
        var command = new CancelOrderCommand(order.Id);

        var result = command.Authorize(admin, order);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Authorize_Denies_NonOwner_WithoutReadAll()
    {
        var order = ArrangeOrder("actor-owner");
        var stranger = await NewActor("actor-stranger", Permissions.OrdersCancel);
        var command = new CancelOrderCommand(order.Id);

        var iResult = command.Authorize(stranger, order);

        iResult.IsSuccess.Should().BeFalse();
        iResult.Error.Should().BeOfType<Error.Forbidden>();
        ((Error.Forbidden)iResult.Error!).PolicyId.Should().Be("orders.cancel.owner-or-admin");
    }

    private static Order ArrangeOrder(string ownerActorId)
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 12, 0, 0, 0, TimeSpan.Zero));
        var product = new Product(
            ProductName.Create("Widget"),
            Sku.Create("WIDGET01"),
            UnitPrice.Create(1m));
        product.AddStock(10).IsSuccess.Should().BeTrue();

        var order = new Order(CustomerId.NewUniqueV7(), ActorId.Create(ownerActorId), clock);
        order.AddLineItem(product.Id, product.ProductName, LineItemQuantity.Create(1), product.UnitPrice)
            .IsSuccess.Should().BeTrue();
        return order;
    }

    private static async Task<Actor> NewActor(string id, params string[] permissions)
    {
        var provider = new TestActorProvider(id, permissions);
        var maybe = await provider.GetCurrentActorAsync(CancellationToken.None);
        return maybe.GetValueOrThrow("TestActorProvider always supplies an actor.");
    }
}