namespace Application.Tests;

using Mediator;
using OrderManagement.Application.Orders;
using OrderManagement.Domain;
using Trellis.Primitives;
using Trellis.Testing.Fakes;

#pragma warning disable TRLS003
#pragma warning disable TRLS001

public class CancelOrderAuthorizationTests
{
    private readonly ISender _sender;
    private readonly TestActorProvider _actorProvider;
    private readonly FakeCustomerRepository _customerRepo;
    private readonly FakeProductRepository _productRepo;
    private readonly FakeOrderRepository _orderRepo;

    public CancelOrderAuthorizationTests(
        ISender sender,
        TestActorProvider actorProvider,
        FakeCustomerRepository customerRepo,
        FakeProductRepository productRepo,
        FakeOrderRepository orderRepo)
    {
        _sender = sender;
        _actorProvider = actorProvider;
        _customerRepo = customerRepo;
        _productRepo = productRepo;
        _orderRepo = orderRepo;
    }

    private async Task<Order> CreateAndSeedOrder(string createdByActorId)
    {
        var customerId = CustomerId.NewUniqueV7();
        var customer = Customer.TryCreate(
            FirstName.Create("Test"), LastName.Create("User"),
            EmailAddress.Create($"test-{Guid.NewGuid():N}@example.com"),
            Maybe<PhoneNumber>.None,
            ShippingAddress.TryCreate(Street.Create("1 St"), City.Create("City"), State.Create("ST"), PostalCode.Create("00000"), Country.Create("US")).Value).Value;
        await _customerRepo.SaveAsync(customer, CancellationToken.None);

        var productId = ProductId.NewUniqueV7();
        var product = Product.TryCreate(ProductName.Create("Widget"), Sku.Create($"WGT-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}"), Money.Create(10m, "USD")).Value;
        product.AddStock(StockQuantity.Create(100));
        await _productRepo.SaveAsync(product, CancellationToken.None);

        var lineItem = new LineItem(product.Id, product.ProductName, LineItemQuantity.Create(1), product.UnitPrice);
        var order = Order.TryCreate(customer.Id, createdByActorId, [lineItem]).Value;
        await _orderRepo.SaveAsync(order, CancellationToken.None);
        return order;
    }

    [Fact]
    public async Task Cancel_by_owner_succeeds()
    {
        var order = await CreateAndSeedOrder("owner-1");
        await using var scope = _actorProvider.WithActor("owner-1", Permissions.OrdersCancel);

        var result = await _sender.Send(new CancelOrderCommand(order.Id));

        result.Should().BeSuccess();
    }

    [Fact]
    public async Task Cancel_by_admin_succeeds()
    {
        var order = await CreateAndSeedOrder("someone-else");
        await using var scope = _actorProvider.WithActor("admin-user", Permissions.OrdersCancel, Permissions.OrdersReadAll);

        var result = await _sender.Send(new CancelOrderCommand(order.Id));

        result.Should().BeSuccess();
    }

    [Fact]
    public async Task Cancel_by_non_owner_without_admin_returns_forbidden()
    {
        var order = await CreateAndSeedOrder("owner-1");
        await using var scope = _actorProvider.WithActor("other-user", Permissions.OrdersCancel);

        var result = await _sender.Send(new CancelOrderCommand(order.Id));

        result.Should().BeFailure()
            .Which.Should().BeOfType<ForbiddenError>();
    }

    [Fact]
    public async Task Cancel_without_cancel_permission_returns_forbidden()
    {
        var order = await CreateAndSeedOrder("owner-1");
        await using var scope = _actorProvider.WithActor("owner-1", Permissions.OrdersRead);

        var result = await _sender.Send(new CancelOrderCommand(order.Id));

        result.Should().BeFailure()
            .Which.Should().BeOfType<ForbiddenError>();
    }
}
