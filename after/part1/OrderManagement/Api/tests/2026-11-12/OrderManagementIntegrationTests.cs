namespace Api.Tests._2026_11_12;

using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Trellis.Testing;

[Collection(TestWebApplicationFactoryCollectionFixture.Id)]
public class OrderManagementIntegrationTests
{
    private readonly TestWebApplicationFactoryFixture _factory;
    private const string V = "api-version=2026-11-12";

    public OrderManagementIntegrationTests(TestWebApplicationFactoryFixture factory, ITestOutputHelper output)
    {
        _factory = factory;
        _factory.OutputHelper = output;
    }

    private HttpClient CreateClient(string actorId = "test-user", params string[] permissions) =>
        _factory.CreateClientWithActor(actorId, permissions);

    private static readonly string[] AllPermissions =
    [
        "customers:create", "customers:read",
        "products:create", "products:read", "products:manage-stock",
        "orders:create", "orders:submit", "orders:approve", "orders:ship",
        "orders:deliver", "orders:cancel", "orders:read", "orders:read-all"
    ];

    private static async Task<CustomerResponse> CreateTestCustomer(HttpClient client, string email)
    {
        var body = new
        {
            firstName = "John",
            lastName = "Doe",
            email,
            shippingAddress = new { street = "123 Main", city = "Springfield", state = "IL", postalCode = "62701", country = "USA" }
        };
        var response = await client.PostAsJsonAsync($"api/Customers?{V}", body, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadAsAsyncWithAssertion<CustomerResponse>());
    }

    private static async Task<ProductResponse> CreateTestProduct(HttpClient client, string sku, decimal price = 19.99m)
    {
        var body = new { productName = "Widget Pro", sku, unitPrice = new { amount = price, currency = "USD" } };
        var response = await client.PostAsJsonAsync($"api/Products?{V}", body, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadAsAsyncWithAssertion<ProductResponse>());
    }

    [Fact]
    public async Task CreateCustomer_Returns201WithLocation()
    {
        var client = CreateClient("user-1", AllPermissions);
        var body = new
        {
            firstName = "Jane",
            lastName = "Smith",
            email = $"jane-{Guid.NewGuid():N}@example.com",
            phoneNumber = "+12025551234",
            shippingAddress = new { street = "456 Oak Ave", city = "Portland", state = "OR", postalCode = "97201", country = "USA" }
        };

        var response = await client.PostAsJsonAsync($"api/Customers?{V}", body, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        var customer = await response.Content.ReadAsAsyncWithAssertion<CustomerResponse>();
        customer.FirstName.Should().Be("Jane");
        customer.Email.Should().Contain("jane-");
        customer.PhoneNumber.Should().Be("+12025551234");
    }

    [Fact]
    public async Task DuplicateEmail_Returns409()
    {
        var client = CreateClient("user-1", AllPermissions);
        var email = $"dup-{Guid.NewGuid():N}@example.com";
        await CreateTestCustomer(client, email);

        var body = new
        {
            firstName = "Other",
            lastName = "Person",
            email,
            shippingAddress = new { street = "789 Elm", city = "Austin", state = "TX", postalCode = "73301", country = "USA" }
        };
        var response = await client.PostAsJsonAsync($"api/Customers?{V}", body, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateProduct_Returns201()
    {
        var client = CreateClient("user-1", AllPermissions);
        var sku = $"TST-{Guid.NewGuid().ToString("N")[..6]}".ToUpper(CultureInfo.InvariantCulture);
        var body = new { productName = "Gadget", sku, unitPrice = new { amount = 49.99, currency = "USD" } };

        var response = await client.PostAsJsonAsync($"api/Products?{V}", body, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task MissingPermission_Returns403()
    {
        var client = CreateClient("user-1", "orders:read"); // no customers:create
        var body = new
        {
            firstName = "No",
            lastName = "Access",
            email = "noaccess@example.com",
            shippingAddress = new { street = "1 St", city = "C", state = "S", postalCode = "1", country = "US" }
        };

        var response = await client.PostAsJsonAsync($"api/Customers?{V}", body, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task FullOrderLifecycle()
    {
        var client = CreateClient("lifecycle-user", AllPermissions);

        // Create customer
        var email = $"lifecycle-{Guid.NewGuid():N}@example.com";
        var customer = await CreateTestCustomer(client, email);

        // Create product
        var sku = $"LC-{Guid.NewGuid().ToString("N")[..6]}".ToUpper(CultureInfo.InvariantCulture);
        var product = await CreateTestProduct(client, sku);

        // Add stock
        var stockResp = await client.PostAsJsonAsync($"api/Products/{product.Id}/stock-additions?{V}",
            new { quantity = 100 }, TestContext.Current.CancellationToken);
        stockResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Create draft order
        var orderBody = new
        {
            customerId = customer.Id,
            lineItems = new[] { new { productId = product.Id, quantity = 2 } }
        };
        var createResp = await client.PostAsJsonAsync($"api/Orders?{V}", orderBody, TestContext.Current.CancellationToken);
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await createResp.Content.ReadAsAsyncWithAssertion<OrderResponse>();
        order.Status.Should().Be("Draft");
        order.CreatedByActorId.Should().Be("lifecycle-user");

        // Submit
        var submitResp = await client.PostAsync($"api/Orders/{order.Id}/submission?{V}", null, TestContext.Current.CancellationToken);
        submitResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var submitted = await submitResp.Content.ReadAsAsyncWithAssertion<OrderResponse>();
        submitted.Status.Should().Be("Submitted");
        submitted.SubmittedAt.Should().NotBeNull();

        // Approve
        var approveResp = await client.PostAsync($"api/Orders/{order.Id}/approval?{V}", null, TestContext.Current.CancellationToken);
        approveResp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await approveResp.Content.ReadAsAsyncWithAssertion<OrderResponse>()).Status.Should().Be("Approved");

        // Ship
        var shipResp = await client.PostAsync($"api/Orders/{order.Id}/shipment?{V}", null, TestContext.Current.CancellationToken);
        shipResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var shipped = await shipResp.Content.ReadAsAsyncWithAssertion<OrderResponse>();
        shipped.Status.Should().Be("Shipped");
        shipped.ShippedAt.Should().NotBeNull();

        // Deliver
        var deliverResp = await client.PostAsync($"api/Orders/{order.Id}/delivery?{V}", null, TestContext.Current.CancellationToken);
        deliverResp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await deliverResp.Content.ReadAsAsyncWithAssertion<OrderResponse>()).Status.Should().Be("Delivered");
    }

    [Fact]
    public async Task CancelByNonOwner_Returns403()
    {
        var ownerClient = CreateClient("owner-cancel", AllPermissions);
        var email = $"canceltest-{Guid.NewGuid():N}@example.com";
        var customer = await CreateTestCustomer(ownerClient, email);
        var sku = $"CN-{Guid.NewGuid().ToString("N")[..6]}".ToUpper(CultureInfo.InvariantCulture);
        var product = await CreateTestProduct(ownerClient, sku);

        var orderBody = new { customerId = customer.Id, lineItems = new[] { new { productId = product.Id, quantity = 1 } } };
        var createResp = await ownerClient.PostAsJsonAsync($"api/Orders?{V}", orderBody, TestContext.Current.CancellationToken);
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await createResp.Content.ReadAsAsyncWithAssertion<OrderResponse>();

        // Try cancel as non-owner without orders:read-all
        var otherClient = CreateClient("other-user", "orders:cancel");
        var cancelResp = await otherClient.PostAsync($"api/Orders/{order.Id}/cancellation?{V}", null, TestContext.Current.CancellationToken);

        cancelResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CancelByOwner_Returns200()
    {
        var client = CreateClient("cancel-owner", AllPermissions);
        var email = $"cancelowner-{Guid.NewGuid():N}@example.com";
        var customer = await CreateTestCustomer(client, email);
        var sku = $"CO-{Guid.NewGuid().ToString("N")[..6]}".ToUpper(CultureInfo.InvariantCulture);
        var product = await CreateTestProduct(client, sku);

        var orderBody = new { customerId = customer.Id, lineItems = new[] { new { productId = product.Id, quantity = 1 } } };
        var createResp = await client.PostAsJsonAsync($"api/Orders?{V}", orderBody, TestContext.Current.CancellationToken);
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await createResp.Content.ReadAsAsyncWithAssertion<OrderResponse>();

        var cancelResp = await client.PostAsync($"api/Orders/{order.Id}/cancellation?{V}", null, TestContext.Current.CancellationToken);

        cancelResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var cancelled = await cancelResp.Content.ReadAsAsyncWithAssertion<OrderResponse>();
        cancelled.Status.Should().Be("Cancelled");
    }

    [Fact]
    public async Task OverdueOrders_Returns200()
    {
        var client = CreateClient("overdue-user", AllPermissions);

        var response = await client.GetAsync($"api/Orders/overdue?{V}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthCheck_Returns200()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MissingApiVersion_Returns400()
    {
        var client = CreateClient("user-1", AllPermissions);

        var response = await client.GetAsync("api/Orders/overdue", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
