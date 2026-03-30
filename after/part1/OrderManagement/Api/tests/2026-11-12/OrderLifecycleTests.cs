namespace Api.Tests.v2026_11_12;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OrderManagement.Domain;
using Trellis.Testing;

[Collection(WebApplicationFixtureCollection.Name)]
public class OrderLifecycleTests : IAsyncLifetime
{
    private readonly TestWebApplicationFactoryFixture _factory;

    public OrderLifecycleTests(TestWebApplicationFactoryFixture factory) => _factory = factory;

    public async ValueTask InitializeAsync() => await _factory.EnsureDatabaseCreatedAsync();
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private const string V = "?api-version=2026-11-12";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private async Task<Guid> CreateCustomer(HttpClient client)
    {
        var request = new
        {
            firstName = "Test",
            lastName = "Customer",
            email = $"cust-{Guid.NewGuid():N}@example.com",
            shippingAddress = new { street = "1 Main", city = "Testville", state = "TX", postalCode = "75001", country = "US" }
        };
        var response = await client.PostAsJsonAsync($"/api/customers{V}", request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateProduct(HttpClient client, string sku, decimal price = 25.00m)
    {
        var request = new
        {
            productName = "Test Product",
            sku,
            unitPrice = new { amount = price, currency = "USD" }
        };
        var response = await client.PostAsJsonAsync($"/api/products{V}", request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    private async Task AddStock(HttpClient client, Guid productId, int quantity)
    {
        var request = new { quantity };
        var response = await client.PostAsJsonAsync($"/api/products/{productId}/stock-additions{V}", request);
        response.EnsureSuccessStatusCode();
    }

    private async Task<Guid> CreateDraftOrder(HttpClient client, Guid customerId, Guid productId, int quantity = 2)
    {
        var request = new
        {
            customerId,
            lineItems = new[] { new { productId, quantity } }
        };
        var response = await client.PostAsJsonAsync($"/api/orders{V}", request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Full_order_lifecycle()
    {
        var client = _factory.CreateClientWithActor("admin",
            Permissions.CustomersCreate, Permissions.CustomersRead,
            Permissions.ProductsCreate, Permissions.ProductsRead, Permissions.ProductsManageStock,
            Permissions.OrdersCreate, Permissions.OrdersRead, Permissions.OrdersReadAll,
            Permissions.OrdersSubmit, Permissions.OrdersApprove, Permissions.OrdersShip,
            Permissions.OrdersDeliver, Permissions.OrdersCancel);

        var sku = $"TST-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        var customerId = await CreateCustomer(client);
        var productId = await CreateProduct(client, sku);
        await AddStock(client, productId, 50);

        // Create draft order
        var orderId = await CreateDraftOrder(client, customerId, productId, 3);

        // Submit
        var submitResponse = await client.PostAsync($"/api/orders/{orderId}/submission{V}", null);
        submitResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var submitBody = await submitResponse.Content.ReadFromJsonAsync<JsonElement>();
        submitBody.GetProperty("status").GetString().Should().Be("Submitted");

        // Approve
        var approveResponse = await client.PostAsync($"/api/orders/{orderId}/approval{V}", null);
        approveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Ship
        var shipResponse = await client.PostAsync($"/api/orders/{orderId}/shipment{V}", null);
        shipResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Deliver
        var deliverResponse = await client.PostAsync($"/api/orders/{orderId}/delivery{V}", null);
        deliverResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var deliverBody = await deliverResponse.Content.ReadFromJsonAsync<JsonElement>();
        deliverBody.GetProperty("status").GetString().Should().Be("Delivered");
    }

    [Fact]
    public async Task Cancel_by_non_owner_returns_403()
    {
        var ownerClient = _factory.CreateClientWithActor("owner-1",
            Permissions.CustomersCreate, Permissions.CustomersRead,
            Permissions.ProductsCreate, Permissions.ProductsManageStock,
            Permissions.OrdersCreate, Permissions.OrdersRead, Permissions.OrdersCancel);

        var sku = $"TST-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        var customerId = await CreateCustomer(ownerClient);
        var productId = await CreateProduct(ownerClient, sku);
        await AddStock(ownerClient, productId, 50);
        var orderId = await CreateDraftOrder(ownerClient, customerId, productId);

        // Different user without OrdersReadAll tries to cancel
        var otherClient = _factory.CreateClientWithActor("other-user", Permissions.OrdersCancel);
        var response = await otherClient.PostAsync($"/api/orders/{orderId}/cancellation{V}", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Cancel_by_owner_returns_200()
    {
        var client = _factory.CreateClientWithActor("owner-2",
            Permissions.CustomersCreate, Permissions.CustomersRead,
            Permissions.ProductsCreate, Permissions.ProductsManageStock,
            Permissions.OrdersCreate, Permissions.OrdersRead, Permissions.OrdersCancel);

        var sku = $"TST-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        var customerId = await CreateCustomer(client);
        var productId = await CreateProduct(client, sku);
        await AddStock(client, productId, 50);
        var orderId = await CreateDraftOrder(client, customerId, productId);

        var response = await client.PostAsync($"/api/orders/{orderId}/cancellation{V}", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_check_returns_200()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Missing_api_version_returns_400()
    {
        var client = _factory.CreateClientWithActor("admin", Permissions.CustomersRead);
        var response = await client.GetAsync("/api/customers/" + Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
