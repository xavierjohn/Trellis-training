namespace Application.Tests;

using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrderManagement.Application;
using OrderManagement.Application.Orders;
using OrderManagement.Domain;
using Trellis.Authorization;
using Trellis.Primitives;
using Trellis.Testing;

public class ActorSnapshotTests
{
    [Fact]
    public async Task CreateDraftOrder_ChangingProvider_UsesCheckedActorOnce()
    {
        var actors = new ChangingActorProvider(Permissions.OrdersCreate);
        using var provider = BuildHost(actors);
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var customer = new Customer(
            FirstName.Create("Ada"),
            LastName.Create("Lovelace"),
            EmailAddress.Create("ada@example.com"),
            Maybe<PhoneNumber>.None,
            new ShippingAddress(
                Street.Create("1 Compute Way"), City.Create("Palo Alto"),
                StateRegion.Create("CA"), PostalCode.Create("94301"), Country.Create("USA")));
        var product = new Product(ProductName.Create("Widget"), Sku.Create("WIDGET01"), UnitPrice.Create(1m));
        services.GetRequiredService<FakeRepository<Customer, CustomerId>>().Add(customer);
        services.GetRequiredService<FakeRepository<Product, ProductId>>().Add(product);

        var result = await services.GetRequiredService<ISender>().Send(
            new CreateDraftOrderCommand(customer.Id, [new DraftLineItem(product.Id, LineItemQuantity.Create(1))]),
            TestContext.Current.CancellationToken);

        result.Should().BeSuccess();
        result.Unwrap().CreatedByActorId.Should().Be(actors.CheckedActor.Id);
        actors.Calls.Should().Be(1);
    }

    [Fact]
    public async Task CancelOrder_ChangingProvider_ReusesStaticAuthorizationActor()
    {
        var actors = new ChangingActorProvider(Permissions.OrdersCancel);
        using var provider = BuildHost(actors);
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var product = new Product(ProductName.Create("Widget"), Sku.Create("WIDGET01"), UnitPrice.Create(1m));
        var order = new Order(CustomerId.NewUniqueV7(), actors.CheckedActor.Id, TimeProvider.System);
        order.AddLineItem(product.Id, product.ProductName, LineItemQuantity.Create(1), product.UnitPrice)
            .Should().BeSuccess();
        services.GetRequiredService<FakeRepository<Order, OrderId>>().Add(order);
        services.GetRequiredService<FakeRepository<Product, ProductId>>().Add(product);

        var result = await services.GetRequiredService<ISender>().Send(
            new CancelOrderCommand(order.Id), TestContext.Current.CancellationToken);

        result.Should().BeSuccess();
        result.Unwrap().Should().BeSameAs(order);
        order.Status.Should().Be(OrderStatus.Cancelled);
        actors.Calls.Should().Be(1);
    }

    private static ServiceProvider BuildHost(IActorProvider actors)
    {
        var services = new ServiceCollection();
        services.AddApplication().AddMockDependencies();
        services.RemoveAll<IActorProvider>();
        services.AddSingleton(actors);
        return services.BuildServiceProvider();
    }

    private sealed class ChangingActorProvider(string permission) : IActorProvider
    {
        public Actor CheckedActor { get; } = Actor.Create("checked-actor", new HashSet<string> { permission });
        public int Calls { get; private set; }

        public Task<Maybe<Actor>> GetCurrentActorAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return Task.FromResult(Maybe.From(Calls == 1
                ? CheckedActor
                : Actor.Create("different-actor", new HashSet<string> { permission })));
        }
    }
}
