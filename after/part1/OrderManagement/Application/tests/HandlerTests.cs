#pragma warning disable TRLS001, TRLS003

namespace Application.Tests;

using Mediator;
using OrderManagement.Application.Customers;
using OrderManagement.Application.Orders;
using OrderManagement.Application.Products;
using OrderManagement.Domain;
using Trellis.Primitives;
using Trellis.Testing;
using Trellis.Testing.Fakes;

public class HandlerTests
{
    private readonly ISender _sender;
    private readonly TestActorProvider _actorProvider;
    private readonly FakeRepository<Customer, CustomerId> _customerRepo;
    private readonly FakeRepository<Product, ProductId> _productRepo;
    private readonly FakeRepository<Order, OrderId> _orderRepo;

    public HandlerTests(
        ISender sender,
        TestActorProvider actorProvider,
        FakeRepository<Customer, CustomerId> customerRepo,
        FakeRepository<Product, ProductId> productRepo,
        FakeRepository<Order, OrderId> orderRepo)
    {
        _sender = sender;
        _actorProvider = actorProvider;
        _customerRepo = customerRepo;
        _productRepo = productRepo;
        _orderRepo = orderRepo;

        _customerRepo.Clear();
        _productRepo.Clear();
        _orderRepo.Clear();
    }

    private static CancellationToken CT => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateCustomer_Valid_ReturnsSuccess()
    {
        var address = ShippingAddress.TryCreate(
            Street.Create("123 Main"), City.Create("City"),
            State.Create("ST"), PostalCode.Create("12345"), Country.Create("USA")).Value;

        var result = await _sender.Send(new CreateCustomerCommand(
            FirstName.Create("John"), LastName.Create("Doe"),
            EmailAddress.Create("john@example.com"), Maybe<PhoneNumber>.None, address), CT);

        result.Should().BeSuccess();
        result.Value.FirstName.Value.Should().Be("John");
    }

    [Fact]
    public async Task CreateCustomer_WithoutPermission_ReturnsForbidden()
    {
        await using var scope = _actorProvider.WithActor("user-1");
        var address = ShippingAddress.TryCreate(
            Street.Create("123 Main"), City.Create("City"),
            State.Create("ST"), PostalCode.Create("12345"), Country.Create("USA")).Value;

        var result = await _sender.Send(new CreateCustomerCommand(
            FirstName.Create("John"), LastName.Create("Doe"),
            EmailAddress.Create("denied@example.com"), Maybe<PhoneNumber>.None, address), CT);

        result.Should().BeFailureOfType<ForbiddenError>();
    }

    [Fact]
    public async Task CreateProduct_Valid_ReturnsSuccess()
    {
        var result = await _sender.Send(new CreateProductCommand(
            ProductName.Create("Widget"), Sku.Create("WGT-001"), Money.Create(19.99m, "USD")), CT);

        result.Should().BeSuccess();
        result.Value.ProductName.Value.Should().Be("Widget");
    }

    [Fact]
    public async Task AddStock_ProductNotFound_ReturnsNotFound()
    {
        var result = await _sender.Send(new AddStockCommand(
            ProductId.NewUniqueV7(), StockQuantity.Create(10)), CT);

        result.Should().BeFailureOfType<NotFoundError>();
    }

    [Fact]
    public async Task GetOrderById_NotFound_ReturnsNotFound()
    {
        var result = await _sender.Send(new GetOrderByIdQuery(OrderId.NewUniqueV7()), CT);

        result.Should().BeFailureOfType<NotFoundError>();
    }

    [Fact]
    public async Task CancelOrder_ByOwner_Succeeds()
    {
        var address = ShippingAddress.TryCreate(
            Street.Create("123 Main"), City.Create("City"),
            State.Create("ST"), PostalCode.Create("12345"), Country.Create("USA")).Value;
        var customerResult = await _sender.Send(new CreateCustomerCommand(
            FirstName.Create("John"), LastName.Create("Doe"),
            EmailAddress.Create("owner-cancel@example.com"), Maybe<PhoneNumber>.None, address), CT);
        customerResult.Should().BeSuccess();

        var productResult = await _sender.Send(new CreateProductCommand(
            ProductName.Create("Widget"), Sku.Create("CANCEL-01"), Money.Create(10m, "USD")), CT);
        productResult.Should().BeSuccess();

        var draftResult = await _sender.Send(new CreateDraftOrderCommand(
            customerResult.Value.Id, [new CreateOrderLineItem(productResult.Value.Id, LineItemQuantity.Create(1))]), CT);
        draftResult.Should().BeSuccess();

        var cancelResult = await _sender.Send(new CancelOrderCommand(draftResult.Value.Id), CT);

        cancelResult.Should().BeSuccess();
        cancelResult.Value.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task CancelOrder_ByNonOwner_ReturnsForbidden()
    {
        var address = ShippingAddress.TryCreate(
            Street.Create("123 Main"), City.Create("City"),
            State.Create("ST"), PostalCode.Create("12345"), Country.Create("USA")).Value;
        var customerResult = await _sender.Send(new CreateCustomerCommand(
            FirstName.Create("Jane"), LastName.Create("Doe"),
            EmailAddress.Create("nonowner@example.com"), Maybe<PhoneNumber>.None, address), CT);
        customerResult.Should().BeSuccess();

        var productResult = await _sender.Send(new CreateProductCommand(
            ProductName.Create("Gadget"), Sku.Create("NOTOWN-01"), Money.Create(5m, "USD")), CT);
        productResult.Should().BeSuccess();

        var draftResult = await _sender.Send(new CreateDraftOrderCommand(
            customerResult.Value.Id, [new CreateOrderLineItem(productResult.Value.Id, LineItemQuantity.Create(1))]), CT);
        draftResult.Should().BeSuccess();

        await using var scope = _actorProvider.WithActor("other-user", Permissions.OrdersCancel);
        var cancelResult = await _sender.Send(new CancelOrderCommand(draftResult.Value.Id), CT);

        cancelResult.Should().BeFailureOfType<ForbiddenError>();
    }
}
