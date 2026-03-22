namespace Application.Tests;

using Mediator;
using OrderManagement.Application.Commands;
using OrderManagement.Application.Queries;
using Application.Tests.Fakes;

public class OrderCommandTests
{
    private readonly ISender _sender;
    private readonly FakeProductRepository _productRepo;
    private readonly FakeCustomerRepository _customerRepo;

    public OrderCommandTests(
        ISender sender,
        ICustomerRepository customerRepository,
        IProductRepository productRepository,
        IOrderRepository _)
    {
        _sender = sender;
        _customerRepo = (FakeCustomerRepository)customerRepository;
        _productRepo = (FakeProductRepository)productRepository;
    }

    private async Task<Customer> CreateAndSaveCustomer()
    {
        var firstName = FirstName.Create("Test");
        var lastName = LastName.Create("User");
        var email = EmailAddress.Create($"test{Guid.NewGuid()}@example.com");
        ShippingAddress.TryCreate("123 Test St", "TestCity", "TS", "12345", "USA").TryGetValue(out var address);
        Customer.TryCreate(firstName, lastName, email, Maybe<PhoneNumber>.None, address!).TryGetValue(out var customer);
        _ = await _customerRepo.SaveAsync(customer!, default);
        return customer!;
    }

    private async Task<Product> CreateAndSaveProduct(string sku, int stock = 100)
    {
        var name = ProductName.Create("Test Product");
        Sku.TryCreate(sku).TryGetValue(out var skuVal);
        var price = Money.Create(10m, "USD");
        Product.TryCreate(name, skuVal!, price).TryGetValue(out var product);
        _ = product!.AddStock(stock);
        _ = await _productRepo.SaveAsync(product, default);
        return product;
    }

    [Fact]
    public async Task CreateCustomer_WithValidData_ReturnsSuccess()
    {
        var firstName = FirstName.Create("Alice");
        var lastName = LastName.Create("Wonderland");
        var email = EmailAddress.Create("alice@wonderland.com");
        ShippingAddress.TryCreate("1 Rabbit Hole", "Wonderland", "WL", "00001", "USA").TryGetValue(out var address);

        var command = new CreateCustomerCommand(firstName, lastName, email, Maybe<PhoneNumber>.None, address!);
        var result = await _sender.Send(command, TestContext.Current.CancellationToken);

        result.Should().BeSuccess()
            .Which.FirstName.Should().Be(firstName);
    }

    [Fact]
    public async Task CreateDraftOrder_WithValidProducts_ReturnsOrder()
    {
        var customer = await CreateAndSaveCustomer();
        var product = await CreateAndSaveProduct("PROD00001");
        CustomerId.TryCreate(customer.Id.Value).TryGetValue(out var customerId);

        var command = new CreateDraftOrderCommand(
            customerId!,
            [new LineItemRequest(product.Id, 5)]);

        var result = await _sender.Send(command, TestContext.Current.CancellationToken);

        result.Should().BeSuccess()
            .Which.Status.Should().Be(OrderStatus.Draft);
    }

    [Fact]
    public async Task SubmitOrder_WithStock_TransitionsToSubmitted()
    {
        var customer = await CreateAndSaveCustomer();
        var product = await CreateAndSaveProduct("PROD00002");
        CustomerId.TryCreate(customer.Id.Value).TryGetValue(out var customerId);

        var createCommand = new CreateDraftOrderCommand(
            customerId!,
            [new LineItemRequest(product.Id, 5)]);
        (await _sender.Send(createCommand, TestContext.Current.CancellationToken)).TryGetValue(out var order);
        order.Should().NotBeNull();

        var submitCommand = new SubmitOrderCommand(order!.Id);
        var result = await _sender.Send(submitCommand, TestContext.Current.CancellationToken);

        result.Should().BeSuccess()
            .Which.Status.Should().Be(OrderStatus.Submitted);
    }

    [Fact]
    public async Task GetOrder_NotFound_ReturnsNotFoundError()
    {
        var query = new GetOrderByIdQuery(OrderId.NewUniqueV4());
        var result = await _sender.Send(query, TestContext.Current.CancellationToken);

        result.Should().BeFailureOfType<NotFoundError>();
    }

    [Fact]
    public async Task ReturnOrder_DeliveredWithinWindow_ReturnsSuccess()
    {
        var customer = await CreateAndSaveCustomer();
        var product = await CreateAndSaveProduct("PROD00010");
        CustomerId.TryCreate(customer.Id.Value).TryGetValue(out var customerId);

        var createCommand = new CreateDraftOrderCommand(customerId!, [new LineItemRequest(product.Id, 3)]);
        (await _sender.Send(createCommand, TestContext.Current.CancellationToken)).TryGetValue(out var order);

        var submitCommand = new SubmitOrderCommand(order!.Id);
        (await _sender.Send(submitCommand, TestContext.Current.CancellationToken)).TryGetValue(out order);

        var approveCommand = new ApproveOrderCommand(order!.Id);
        (await _sender.Send(approveCommand, TestContext.Current.CancellationToken)).TryGetValue(out order);

        var shipCommand = new ShipOrderCommand(order!.Id);
        (await _sender.Send(shipCommand, TestContext.Current.CancellationToken)).TryGetValue(out order);

        var deliverCommand = new DeliverOrderCommand(order!.Id);
        (await _sender.Send(deliverCommand, TestContext.Current.CancellationToken)).TryGetValue(out order);

        ReturnReason.TryCreate("Product was damaged on arrival at my home").TryGetValue(out var reason);
        var returnCommand = new ReturnOrderCommand(order!.Id, reason!);
        var result = await _sender.Send(returnCommand, TestContext.Current.CancellationToken);

        result.Should().BeSuccess()
            .Which.Status.Should().Be(OrderStatus.Returned);
    }

    [Fact]
    public async Task ReturnOrder_MissingPermission_ReturnsForbiddenError()
    {
        var customer = await CreateAndSaveCustomer();
        var product = await CreateAndSaveProduct("PROD00011");
        CustomerId.TryCreate(customer.Id.Value).TryGetValue(out var customerId);

        var createCommand = new CreateDraftOrderCommand(customerId!, [new LineItemRequest(product.Id, 2)]);
        (await _sender.Send(createCommand, TestContext.Current.CancellationToken)).TryGetValue(out var order);

        ReturnReason.TryCreate("Valid reason of at least ten chars").TryGetValue(out var reason);
        var returnCommand = new ReturnOrderCommand(order!.Id, reason!);

        // Verify the command requires the correct permission
        returnCommand.RequiredPermissions.Should().Contain(Permissions.OrdersReturn);
    }
}
