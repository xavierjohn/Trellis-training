namespace Api.Tests._2026_11_12;

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Trellis.Testing;

[Collection(TestWebApplicationFactoryCollectionFixture.Id)]
public class OrderManagementControllerTests
{
    private readonly TestWebApplicationFactoryFixture _factory;

    private const string ApiVersion = "2026-11-12";
    private const string VersionParam = $"api-version={ApiVersion}";

    public OrderManagementControllerTests(TestWebApplicationFactoryFixture factory, ITestOutputHelper output)
    {
        _factory = factory;
        _factory.OutputHelper = output;
    }

    private HttpClient CreateClient(string actorId, params string[] permissions) =>
        _factory.CreateClientWithActor(actorId, permissions);

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static object CustomerBody(string email) => new
    {
        firstName = "Jane",
        lastName = "Smith",
        email,
        shippingAddress = new
        {
            street = "1 Oak Ave",
            city = "Portland",
            state = "OR",
            postalCode = "97201",
            country = "US"
        }
    };

    private static object ProductBody(string sku) => new
    {
        productName = "Test Widget",
        sku,
        unitPrice = new { amount = 9.99m, currency = "USD" }
    };

    private static async Task<CustomerResponse> CreateCustomerAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync($"api/Customers?{VersionParam}", CustomerBody(email), TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await response.Content.ReadAsAsyncWithAssertion<CustomerResponse>();
    }

    private static async Task<ProductResponse> CreateProductAsync(HttpClient client, string sku)
    {
        var response = await client.PostAsJsonAsync($"api/Products?{VersionParam}", ProductBody(sku), TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await response.Content.ReadAsAsyncWithAssertion<ProductResponse>();
    }

    private static async Task AddStockAsync(HttpClient client, Guid productId, int quantity)
    {
        var response = await client.PostAsJsonAsync(
            $"api/Products/{productId}/stock-additions?{VersionParam}",
            new { quantity },
            TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<OrderResponse> CreateOrderAsync(HttpClient client, Guid customerId, Guid productId, int qty = 2)
    {
        var body = new
        {
            customerId,
            lineItems = new[] { new { productId, quantity = qty } }
        };
        var response = await client.PostAsJsonAsync($"api/Orders?{VersionParam}", body, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await response.Content.ReadAsAsyncWithAssertion<OrderResponse>();
    }

    // ─── Customer Tests ───────────────────────────────────────────────────────

    [Fact]
    public async Task Create_customer_returns_201_with_location()
    {
        var client = CreateClient("user-1", "customers:create", "customers:read");
        var uniqueEmail = $"create-{Guid.NewGuid():N}@example.com";

        var response = await client.PostAsJsonAsync($"api/Customers?{VersionParam}", CustomerBody(uniqueEmail), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        var customer = await response.Content.ReadAsAsyncWithAssertion<CustomerResponse>();
        customer.FirstName.Should().Be("Jane");
        customer.Email.Should().Be(uniqueEmail);
    }

    [Fact]
    public async Task Create_customer_duplicate_email_returns_409()
    {
        var client = CreateClient("user-1", "customers:create");
        var email = $"dup-{Guid.NewGuid():N}@example.com";

        await client.PostAsJsonAsync($"api/Customers?{VersionParam}", CustomerBody(email), TestContext.Current.CancellationToken);
        var response = await client.PostAsJsonAsync($"api/Customers?{VersionParam}", CustomerBody(email), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_customer_without_permission_returns_403()
    {
        var client = CreateClient("user-1", "customers:read");

        var response = await client.PostAsJsonAsync($"api/Customers?{VersionParam}", CustomerBody("no-perm@example.com"), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ─── Full Order Lifecycle ─────────────────────────────────────────────────

    [Fact]
    public async Task Full_order_lifecycle_create_submit_approve_ship_deliver()
    {
        var allPerms = new[]
        {
            "customers:create", "customers:read",
            "products:create", "products:read", "products:manage-stock",
            "orders:create", "orders:submit", "orders:approve",
            "orders:ship", "orders:deliver", "orders:cancel",
            "orders:read", "orders:read-all"
        };

        var client = CreateClient("lifecycle-user", allPerms);
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"lifecycle-{uniqueSuffix}@example.com";
        var sku = $"LC{uniqueSuffix[..6].ToUpperInvariant()}";

        // 1. Create customer
        var customer = await CreateCustomerAsync(client, email);
        customer.Id.Should().NotBeEmpty();

        // 2. Create product
        var product = await CreateProductAsync(client, sku);
        product.Id.Should().NotBeEmpty();
        product.StockQuantity.Should().Be(0);

        // 3. Add stock
        await AddStockAsync(client, product.Id, 50);

        // 4. Create draft order
        var order = await CreateOrderAsync(client, customer.Id, product.Id, qty: 2);
        order.Status.Should().Be("Draft");
        order.LineItems.Should().HaveCount(1);

        // 5. Submit order
        var submitResp = await client.PostAsync($"api/Orders/{order.Id}/submission?{VersionParam}", null, TestContext.Current.CancellationToken);
        submitResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var submitted = await submitResp.Content.ReadAsAsyncWithAssertion<OrderResponse>();
        submitted.Status.Should().Be("Submitted");
        submitted.SubmittedAt.Should().NotBeNull();

        // 6. Approve order
        var approveResp = await client.PostAsync($"api/Orders/{order.Id}/approval?{VersionParam}", null, TestContext.Current.CancellationToken);
        approveResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var approved = await approveResp.Content.ReadAsAsyncWithAssertion<OrderResponse>();
        approved.Status.Should().Be("Approved");

        // 7. Ship order
        var shipResp = await client.PostAsync($"api/Orders/{order.Id}/shipment?{VersionParam}", null, TestContext.Current.CancellationToken);
        shipResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var shipped = await shipResp.Content.ReadAsAsyncWithAssertion<OrderResponse>();
        shipped.Status.Should().Be("Shipped");
        shipped.ShippedAt.Should().NotBeNull();

        // 8. Deliver order
        var deliverResp = await client.PostAsync($"api/Orders/{order.Id}/delivery?{VersionParam}", null, TestContext.Current.CancellationToken);
        deliverResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var delivered = await deliverResp.Content.ReadAsAsyncWithAssertion<OrderResponse>();
        delivered.Status.Should().Be("Delivered");
    }

    // ─── Cancel Tests ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Cancel_by_non_owner_returns_403()
    {
        var ownerClient = CreateClient("owner-1", "customers:create", "products:create", "products:manage-stock", "orders:create", "orders:cancel");
        var otherClient = CreateClient("other-user", "orders:cancel");

        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var customer = await CreateCustomerAsync(ownerClient, $"owner1-{uniqueSuffix}@example.com");
        var product = await CreateProductAsync(ownerClient, $"OW{uniqueSuffix[..6].ToUpperInvariant()}");
        await AddStockAsync(ownerClient, product.Id, 10);
        var order = await CreateOrderAsync(ownerClient, customer.Id, product.Id);

        var response = await otherClient.PostAsync($"api/Orders/{order.Id}/cancellation?{VersionParam}", null, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Cancel_by_owner_returns_200()
    {
        var client = CreateClient("owner-2", "customers:create", "products:create", "products:manage-stock", "orders:create", "orders:cancel");

        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var customer = await CreateCustomerAsync(client, $"owner2-{uniqueSuffix}@example.com");
        var product = await CreateProductAsync(client, $"C2{uniqueSuffix[..6].ToUpperInvariant()}");
        await AddStockAsync(client, product.Id, 10);
        var order = await CreateOrderAsync(client, customer.Id, product.Id);

        var response = await client.PostAsync($"api/Orders/{order.Id}/cancellation?{VersionParam}", null, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var cancelled = await response.Content.ReadAsAsyncWithAssertion<OrderResponse>();
        cancelled.Status.Should().Be("Cancelled");
    }

    // ─── Overdue Orders ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetOverdue_returns_200_with_list()
    {
        var client = CreateClient("admin", "orders:read-all");

        var response = await client.GetAsync($"api/Orders/overdue?{VersionParam}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var orders = await response.Content.ReadAsAsyncWithAssertion<OrderResponse[]>();
        orders.Should().NotBeNull();
        // A freshly submitted order is not overdue (submitted less than 7 days ago)
        orders.Should().NotContain(o => o.Status == "Draft");
    }

    // ─── API Versioning ───────────────────────────────────────────────────────

    [Fact]
    public async Task Request_without_api_version_returns_400()
    {
        var client = CreateClient("user-1", "customers:read");

        var response = await client.GetAsync("api/Customers", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ─── Health Endpoint ──────────────────────────────────────────────────────

    [Fact]
    public async Task Health_endpoint_returns_200()
    {
        var client = CreateClient("user-1");

        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
