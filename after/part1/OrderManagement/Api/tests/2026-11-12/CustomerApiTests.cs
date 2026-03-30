namespace Api.Tests.v2026_11_12;

using System.Net;
using System.Net.Http.Json;
using OrderManagement.Domain;
using Trellis.Testing;

[Collection(WebApplicationFixtureCollection.Name)]
public class CustomerApiTests : IAsyncLifetime
{
    private readonly TestWebApplicationFactoryFixture _factory;

    public CustomerApiTests(TestWebApplicationFactoryFixture factory) => _factory = factory;

    public async ValueTask InitializeAsync() => await _factory.EnsureDatabaseCreatedAsync();
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private const string ApiVersion = "?api-version=2026-11-12";

    [Fact]
    public async Task CreateCustomer_returns_201_with_location()
    {
        var client = _factory.CreateClientWithActor("admin", Permissions.CustomersCreate, Permissions.CustomersRead);
        var request = new
        {
            firstName = "John",
            lastName = "Doe",
            email = $"john-{Guid.NewGuid():N}@example.com",
            shippingAddress = new { street = "123 Main", city = "Seattle", state = "WA", postalCode = "98101", country = "US" }
        };

        var response = await client.PostAsJsonAsync($"/api/customers{ApiVersion}", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateCustomer_duplicate_email_returns_409()
    {
        var client = _factory.CreateClientWithActor("admin", Permissions.CustomersCreate);
        var email = $"dup-{Guid.NewGuid():N}@example.com";
        var request = new
        {
            firstName = "Jane",
            lastName = "Doe",
            email,
            shippingAddress = new { street = "456 Oak", city = "Portland", state = "OR", postalCode = "97201", country = "US" }
        };

        await client.PostAsJsonAsync($"/api/customers{ApiVersion}", request);
        var response = await client.PostAsJsonAsync($"/api/customers{ApiVersion}", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateCustomer_missing_permission_returns_403()
    {
        var client = _factory.CreateClientWithActor("no-perms");
        var request = new
        {
            firstName = "Nope",
            lastName = "User",
            email = $"nope-{Guid.NewGuid():N}@example.com",
            shippingAddress = new { street = "1 St", city = "City", state = "ST", postalCode = "00000", country = "US" }
        };

        var response = await client.PostAsJsonAsync($"/api/customers{ApiVersion}", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetCustomer_not_found_returns_404()
    {
        var client = _factory.CreateClientWithActor("admin", Permissions.CustomersRead);
        var response = await client.GetAsync($"/api/customers/{Guid.NewGuid()}{ApiVersion}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
