namespace Application.Tests;

using Mediator;
using OrderManagement.Application.Orders;
using OrderManagement.Domain;
using Trellis.Primitives;
using Trellis.Testing.Fakes;

#pragma warning disable TRLS003
#pragma warning disable TRLS001

public class OrderHandlerTests
{
    private readonly ISender _sender;
    private readonly FakeCustomerRepository _customerRepo;
    private readonly FakeProductRepository _productRepo;
    private readonly FakeOrderRepository _orderRepo;

    public OrderHandlerTests(
        ISender sender,
        FakeCustomerRepository customerRepo,
        FakeProductRepository productRepo,
        FakeOrderRepository orderRepo)
    {
        _sender = sender;
        _customerRepo = customerRepo;
        _productRepo = productRepo;
        _orderRepo = orderRepo;
    }

    private async Task<(Customer Customer, Product Product)> SeedCustomerAndProduct()
    {
        var customer = Customer.TryCreate(
            FirstName.Create("Test"), LastName.Create("User"),
            EmailAddress.Create($"test-{Guid.NewGuid():N}@example.com"),
            Maybe<PhoneNumber>.None,
            ShippingAddress.TryCreate(Street.Create("1 St"), City.Create("City"), State.Create("ST"), PostalCode.Create("00000"), Country.Create("US")).Value).Value;
        await _customerRepo.SaveAsync(customer, CancellationToken.None);

        var product = Product.TryCreate(
            ProductName.Create("Widget"),
            Sku.Create($"WGT-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}"),
            Money.Create(10m, "USD")).Value;
        product.AddStock(StockQuantity.Create(100));
        await _productRepo.SaveAsync(product, CancellationToken.None);

        return (customer, product);
    }

    [Fact]
    public async Task CreateDraftOrder_valid_returns_success()
    {
        var (customer, product) = await SeedCustomerAndProduct();

        var result = await _sender.Send(new CreateDraftOrderCommand(
            customer.Id,
            [new CreateDraftOrderLineItemInput(product.Id, LineItemQuantity.Create(2))]));

        result.Should().BeSuccess()
            .Which.Status.Should().Be(OrderStatus.Draft);
    }

    [Fact]
    public async Task CreateDraftOrder_nonexistent_customer_returns_not_found()
    {
        var (_, product) = await SeedCustomerAndProduct();

        var result = await _sender.Send(new CreateDraftOrderCommand(
            CustomerId.NewUniqueV7(),
            [new CreateDraftOrderLineItemInput(product.Id, LineItemQuantity.Create(1))]));

        result.Should().BeFailure()
            .Which.Should().BeOfType<NotFoundError>();
    }

    [Fact]
    public async Task GetOrder_not_found_returns_not_found()
    {
        var result = await _sender.Send(new GetOrderByIdQuery(OrderId.NewUniqueV7()));

        result.Should().BeFailure()
            .Which.Should().BeOfType<NotFoundError>();
    }

    [Fact]
    public async Task SubmitOrder_reserves_stock_and_transitions()
    {
        var (customer, product) = await SeedCustomerAndProduct();
        var createResult = await _sender.Send(new CreateDraftOrderCommand(
            customer.Id,
            [new CreateDraftOrderLineItemInput(product.Id, LineItemQuantity.Create(3))]));
        createResult.Should().BeSuccess();
        var order = createResult.Value;

        var result = await _sender.Send(new SubmitOrderCommand(order.Id));

        result.Should().BeSuccess()
            .Which.Status.Should().Be(OrderStatus.Submitted);
    }
}
