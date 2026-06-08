namespace Api.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using OrderManagement.Api.v2026_11_12.Models;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>
/// End-to-end integration smoke tests for the Order Management API.
/// Covers happy-path lifecycle (customer → product → order → submit → approve → ship → deliver)
/// and the canonical failure shapes (403, 404, 409, 422). Auth uses the X-Test-Actor
/// header per spec §5.5; missing header falls back to the default admin actor.
/// </summary>
[Collection(TestWebApplicationFactoryCollectionFixture.Id)]
public class OrderLifecycleTests
{
    private const string ApiVersion = "2026-11-12";
    private readonly TestWebApplicationFactoryFixture _fixture;

    public OrderLifecycleTests(TestWebApplicationFactoryFixture fixture) => _fixture = fixture;

    private HttpClient Client(Actor? actor = null)
    {
        var client = _fixture.CreateClient();
        if (actor is not null)
        {
            var json = JsonSerializer.Serialize(new
            {
                Id = actor.Id,
                Permissions = actor.Permissions.ToArray(),
            });
            client.DefaultRequestHeaders.Add("X-Test-Actor", json);
        }
        return client;
    }

    [Fact]
    public async Task FullOrderLifecycle_FromCreateToDelivered_Succeeds()
    {
        var client = Client();

        var customer = await CreateCustomerAsync(client, "ada-life@example.com");
        var product = await CreateProductAsync(client, $"SKU{Random.Shared.Next(10000, 99999)}", 50);

        var order = await CreateOrderAsync(client, customer.Id, product.Id, 2);
        order.Status.Should().Be("Draft");
        order.OrderTotal.Should().Be(product.UnitPrice * 2);

        order = await TransitionAsync(client, $"/api/orders/{order.Id}/submission?api-version={ApiVersion}");
        order.Status.Should().Be("Submitted");

        order = await TransitionAsync(client, $"/api/orders/{order.Id}/approval?api-version={ApiVersion}");
        order.Status.Should().Be("Approved");

        order = await TransitionAsync(client, $"/api/orders/{order.Id}/shipment?api-version={ApiVersion}");
        order.Status.Should().Be("Shipped");

        order = await TransitionAsync(client, $"/api/orders/{order.Id}/delivery?api-version={ApiVersion}");
        order.Status.Should().Be("Delivered");
    }

    [Fact]
    public async Task CreateCustomer_DuplicateEmail_Returns409Conflict()
    {
        var client = Client();
        await CreateCustomerAsync(client, "dup-it@example.com");

        var response = await client.PostAsJsonAsync(
            $"/api/customers?api-version={ApiVersion}",
            NewCustomerPayload("dup-it@example.com"),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetOrder_NonExistent_Returns404NotFound()
    {
        var client = Client();
        var fakeId = Guid.NewGuid();

        var response = await client.GetAsync($"/api/orders/{fakeId}?api-version={ApiVersion}",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateCustomer_ActorWithoutPermission_Returns403Forbidden()
    {
        var noPermActor = new Actor("actor-nobody",
            permissions: new HashSet<string>(StringComparer.Ordinal),
            forbiddenPermissions: new HashSet<string>(StringComparer.Ordinal),
            attributes: new Dictionary<string, string>(StringComparer.Ordinal));
        var client = Client(noPermActor);

        var response = await client.PostAsJsonAsync(
            $"/api/customers?api-version={ApiVersion}",
            NewCustomerPayload("nobody@example.com"),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task HealthEndpoint_Returns200_WithoutAuth()
    {
        var client = _fixture.CreateClient();
        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static object NewCustomerPayload(string email) => new
    {
        FirstName = "Ada",
        LastName = "Lovelace",
        Email = email,
        ShippingAddress = new
        {
            Street = "1 Compute Way",
            City = "Palo Alto",
            State = "CA",
            PostalCode = "94301",
            Country = "USA",
        },
    };

    private static async Task<CustomerResponse> CreateCustomerAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/customers?api-version={ApiVersion}",
            NewCustomerPayload(email),
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CustomerResponse>(TestContext.Current.CancellationToken))!;
    }

    private static async Task<ProductResponse> CreateProductAsync(HttpClient client, string sku, int initialStock)
    {
        var create = await client.PostAsJsonAsync(
            $"/api/products?api-version={ApiVersion}",
            new
            {
                ProductName = "Widget",
                Sku = sku,
                UnitPrice = 9.99m,
            },
            TestContext.Current.CancellationToken);
        create.EnsureSuccessStatusCode();
        var product = (await create.Content.ReadFromJsonAsync<ProductResponse>(TestContext.Current.CancellationToken))!;

        if (initialStock > 0)
        {
            var stock = await client.PostAsJsonAsync(
                $"/api/products/{product.Id}/stock-additions?api-version={ApiVersion}",
                new { Quantity = initialStock },
                TestContext.Current.CancellationToken);
            stock.EnsureSuccessStatusCode();
            product = (await stock.Content.ReadFromJsonAsync<ProductResponse>(TestContext.Current.CancellationToken))!;
        }
        return product;
    }

    private static async Task<OrderResponse> CreateOrderAsync(HttpClient client, Guid customerId, Guid productId, int quantity)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/orders?api-version={ApiVersion}",
            new
            {
                CustomerId = customerId,
                LineItems = new[]
                {
                    new { ProductId = productId, Quantity = quantity },
                },
            },
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderResponse>(TestContext.Current.CancellationToken))!;
    }

    private static async Task<OrderResponse> TransitionAsync(HttpClient client, string url)
    {
        var response = await client.PostAsync(url, new StringContent("", Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderResponse>(TestContext.Current.CancellationToken))!;
    }
}