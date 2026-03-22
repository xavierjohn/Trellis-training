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
}
