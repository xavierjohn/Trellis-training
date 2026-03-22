namespace Api.Tests.v2026_11_12;

using System.Net;
using System.Net.Http.Json;
using OrderManagement.Api.v2026_11_12.Models;
using Trellis.Testing;

[Collection(TestWebApplicationFactoryCollectionFixture.Id)]
public class OrdersIntegrationTests
{
    private readonly TestWebApplicationFactoryFixture _factory;

    public OrdersIntegrationTests(TestWebApplicationFactoryFixture factory, ITestOutputHelper output)
    {
        _factory = factory;
        _factory.OutputHelper = output;
        _factory.EnsureDbCreated();
    }

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        var client = _factory.CreateClientWithActor("admin",
            Permissions.OrdersRead);

        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateProduct_WithValidData_Returns201()
    {
        var client = _factory.CreateClientWithActor("admin",
            Permissions.ProductsCreate);

        var request = new CreateProductRequest(
            "Test Widget",
            "WIDGET001",
            9.99m,
            "USD");

        var response = await client.PostAsJsonAsync(
            "api/Products?api-version=2026-11-12",
            request,
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateCustomer_WithValidData_Returns201()
    {
        var client = _factory.CreateClientWithActor("admin",
            Permissions.CustomersCreate);

        var request = new CreateCustomerRequest(
            "John",
            "Doe",
            $"john.doe.{Guid.NewGuid()}@example.com",
            null,
            "123 Main St",
            "Anytown",
            "CA",
            "12345",
            "USA");

        var response = await client.PostAsJsonAsync(
            "api/Customers?api-version=2026-11-12",
            request,
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateOrder_WithoutPermission_Returns403()
    {
        var client = _factory.CreateClientWithActor("unauthorized-user");

        var request = new CreateDraftOrderRequest(
            Guid.NewGuid(),
            [new AddLineItemRequest(Guid.NewGuid(), 1)]);

        var response = await client.PostAsJsonAsync(
            "api/Orders?api-version=2026-11-12",
            request,
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetOrder_NotFound_Returns404()
    {
        var client = _factory.CreateClientWithActor("admin",
            Permissions.OrdersRead);

        var response = await client.GetAsync(
            $"api/Orders/{Guid.NewGuid()}?api-version=2026-11-12",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<(HttpClient client, Guid orderId)> CreateDeliveredOrderViaApi()
    {
        var client = _factory.CreateClientWithActor("admin",
            Permissions.ProductsCreate,
            Permissions.ProductsManageStock,
            Permissions.CustomersCreate,
            Permissions.OrdersCreate,
            Permissions.OrdersSubmit,
            Permissions.OrdersApprove,
            Permissions.OrdersShip,
            Permissions.OrdersDeliver,
            Permissions.OrdersReturn,
            Permissions.OrdersRead);

        var productResponse = await client.PostAsJsonAsync(
            "api/Products?api-version=2026-11-12",
            new CreateProductRequest("Widget", $"WGT{Guid.NewGuid():N}"[..10].ToUpperInvariant(), 9.99m, "USD"),
            TestContext.Current.CancellationToken);
        var product = await productResponse.Content.ReadFromJsonAsync<ProductDto>(TestContext.Current.CancellationToken);

        await client.PostAsJsonAsync(
            $"api/Products/{product!.Id}/stock?api-version=2026-11-12",
            new AddStockRequest(50),
            TestContext.Current.CancellationToken);

        var customerResponse = await client.PostAsJsonAsync(
            "api/Customers?api-version=2026-11-12",
            new CreateCustomerRequest("Jane", "Doe", $"jane.{Guid.NewGuid()}@test.com", null, "1 St", "City", "ST", "11111", "USA"),
            TestContext.Current.CancellationToken);
        var customer = await customerResponse.Content.ReadFromJsonAsync<CustomerDto>(TestContext.Current.CancellationToken);

        var orderResponse = await client.PostAsJsonAsync(
            "api/Orders?api-version=2026-11-12",
            new CreateDraftOrderRequest(customer!.Id, [new AddLineItemRequest(product.Id, 2)]),
            TestContext.Current.CancellationToken);
        var order = await orderResponse.Content.ReadFromJsonAsync<OrderDto>(TestContext.Current.CancellationToken);

        await client.PostAsJsonAsync($"api/Orders/{order!.Id}/submit?api-version=2026-11-12", (object?)null, TestContext.Current.CancellationToken);
        await client.PostAsJsonAsync($"api/Orders/{order.Id}/approve?api-version=2026-11-12", (object?)null, TestContext.Current.CancellationToken);
        await client.PostAsJsonAsync($"api/Orders/{order.Id}/ship?api-version=2026-11-12", (object?)null, TestContext.Current.CancellationToken);
        await client.PostAsJsonAsync($"api/Orders/{order.Id}/deliver?api-version=2026-11-12", (object?)null, TestContext.Current.CancellationToken);

        return (client, order.Id);
    }

    [Fact]
    public async Task ReturnOrder_DeliveredWithinWindow_Returns200WithReturnedStatus()
    {
        var (client, orderId) = await CreateDeliveredOrderViaApi();

        var response = await client.PostAsJsonAsync(
            $"api/Orders/{orderId}/return?api-version=2026-11-12",
            new ReturnOrderRequest("Product arrived damaged and not working"),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<OrderDto>(TestContext.Current.CancellationToken);
        dto!.Status.Should().Be("Returned");
        dto.ReturnedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ReturnOrder_WithoutPermission_Returns403()
    {
        var client = _factory.CreateClientWithActor("no-permissions-user");

        var response = await client.PostAsJsonAsync(
            $"api/Orders/{Guid.NewGuid()}/return?api-version=2026-11-12",
            new ReturnOrderRequest("Valid return reason of sufficient length"),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReturnOrder_InvalidReason_Returns400()
    {
        var (client, orderId) = await CreateDeliveredOrderViaApi();

        var response = await client.PostAsJsonAsync(
            $"api/Orders/{orderId}/return?api-version=2026-11-12",
            new ReturnOrderRequest("short"),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
