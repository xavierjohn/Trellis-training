namespace Api.Tests;

using System.Net;
using System.Net.Http.Json;
using OrderManagement.Api.v2026_11_12.Models;
using Trellis.Asp;

/// <summary>
/// Integration tests for the bounded-list endpoints (spec §7.1). Exercises the real
/// SQLite cursor (keyset) pagination path end-to-end: the page envelope, the RFC 8288
/// <c>Link</c> header, cursor round-trips with no duplicates or gaps, and the
/// malformed-cursor → 422 contract.
/// </summary>
[Collection(TestWebApplicationFactoryCollectionFixture.Id)]
public class PaginationTests
{
    private const string ApiVersion = "2026-11-12";
    private readonly TestWebApplicationFactoryFixture _fixture;

    public PaginationTests(TestWebApplicationFactoryFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ListOrdersByCustomer_PagesThroughWithCursor_NoDuplicatesOrGaps()
    {
        var client = _fixture.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var customer = await CreateCustomerAsync(client, $"pager-{Guid.NewGuid():N}@example.com");
        var product = await CreateProductAsync(client, $"SKU{Guid.NewGuid():N}".ToUpperInvariant()[..15], 100);

        const int total = 5;
        var created = new HashSet<Guid>();
        for (var i = 0; i < total; i++)
            created.Add((await CreateOrderAsync(client, customer.Id, product.Id, 1)).Id);

        // Walk the pages by following the server-emitted next URL, two at a time.
        var seen = new List<Guid>();
        var url = $"/api/customers/{customer.Id}/orders?api-version={ApiVersion}&limit=2";
        var firstPage = true;
        var guard = 0;

        while (url is not null && guard++ < 10)
        {
            var response = await client.GetAsync(url, ct);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var page = (await response.Content.ReadFromJsonAsync<PagedResponse<OrderResponse>>(ct))!;
            page.RequestedLimit.Should().Be(2);
            page.AppliedLimit.Should().Be(2);
            page.WasCapped.Should().BeFalse();
            page.Items.Count.Should().BeLessThanOrEqualTo(2);
            page.DeliveredCount.Should().Be(page.Items.Count);

            if (firstPage)
            {
                // 5 orders at 2 per page ⇒ the first page must carry a next cursor + Link header.
                page.Next.Should().NotBeNull();
                response.Headers.GetValues("Link").Should().Contain(v => v.Contains("rel=\"next\""));
                firstPage = false;
            }

            seen.AddRange(page.Items.Select(o => o.Id));
            url = page.Next?.Href;
        }

        seen.Should().HaveCount(total);
        seen.Should().OnlyHaveUniqueItems();
        seen.Should().BeEquivalentTo(created);
    }

    [Fact]
    public async Task ListOrdersByCustomer_MalformedCursor_Returns422()
    {
        var client = _fixture.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var customer = await CreateCustomerAsync(client, $"badcursor-{Guid.NewGuid():N}@example.com");

        var response = await client.GetAsync(
            $"/api/customers/{customer.Id}/orders?api-version={ApiVersion}&limit=2&cursor=not-a-valid-cursor",
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task ListOverdue_WithLimit_ReturnsBoundedPageEnvelope()
    {
        var client = _fixture.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.GetAsync(
            $"/api/orders/overdue?api-version={ApiVersion}&limit=2", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = (await response.Content.ReadFromJsonAsync<PagedResponse<OrderResponse>>(ct))!;
        page.RequestedLimit.Should().Be(2);
        page.AppliedLimit.Should().Be(2);
        page.DeliveredCount.Should().Be(page.Items.Count);
    }

    [Fact]
    public async Task ListOverdue_MalformedCursor_Returns422()
    {
        var client = _fixture.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.GetAsync(
            $"/api/orders/overdue?api-version={ApiVersion}&cursor=not-a-valid-cursor", ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private static async Task<CustomerResponse> CreateCustomerAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/customers?api-version={ApiVersion}",
            new
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
            },
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CustomerResponse>(TestContext.Current.CancellationToken))!;
    }

    private static async Task<ProductResponse> CreateProductAsync(HttpClient client, string sku, int initialStock)
    {
        var create = await client.PostAsJsonAsync(
            $"/api/products?api-version={ApiVersion}",
            new { ProductName = "Widget", Sku = sku, UnitPrice = 9.99m },
            TestContext.Current.CancellationToken);
        create.EnsureSuccessStatusCode();
        var product = (await create.Content.ReadFromJsonAsync<ProductResponse>(TestContext.Current.CancellationToken))!;

        var stock = await client.PostAsJsonAsync(
            $"/api/products/{product.Id}/stock-additions?api-version={ApiVersion}",
            new { Quantity = initialStock },
            TestContext.Current.CancellationToken);
        stock.EnsureSuccessStatusCode();
        return (await stock.Content.ReadFromJsonAsync<ProductResponse>(TestContext.Current.CancellationToken))!;
    }

    private static async Task<OrderResponse> CreateOrderAsync(HttpClient client, Guid customerId, Guid productId, int quantity)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/orders?api-version={ApiVersion}",
            new
            {
                CustomerId = customerId,
                LineItems = new[] { new { ProductId = productId, Quantity = quantity } },
            },
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderResponse>(TestContext.Current.CancellationToken))!;
    }
}
