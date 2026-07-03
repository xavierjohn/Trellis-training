namespace Api.Tests;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using OrderManagement.Api.v2026_11_12.Models;

/// <summary>
/// Integration tests for HTTP optimistic concurrency + conditional requests (spec §7.2):
/// GET emits a strong ETag and honors <c>If-None-Match</c> (304); AddLineItem requires an
/// <c>If-Match</c> validator (428 when absent, 412 when stale, 200 when fresh).
/// </summary>
[Collection(TestWebApplicationFactoryCollectionFixture.Id)]
public class ConcurrencyTests
{
    private const string ApiVersion = "2026-11-12";
    private readonly TestWebApplicationFactoryFixture _fixture;

    public ConcurrencyTests(TestWebApplicationFactoryFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetById_EmitsETag_AndHonorsIfNoneMatchWith304()
    {
        var client = _fixture.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var (customerId, productId, _) = await SeedAsync(client, ct);
        var orderId = (await CreateOrderAsync(client, customerId, productId, ct)).Id;

        var get = await client.GetAsync($"/api/orders/{orderId}?api-version={ApiVersion}", ct);
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        get.Headers.ETag.Should().NotBeNull();

        using var conditional = new HttpRequestMessage(HttpMethod.Get, $"/api/orders/{orderId}?api-version={ApiVersion}");
        conditional.Headers.IfNoneMatch.Add(get.Headers.ETag!);
        var notModified = await client.SendAsync(conditional, ct);
        notModified.StatusCode.Should().Be(HttpStatusCode.NotModified);
    }

    [Fact]
    public async Task AddLineItem_RequiresIfMatch_428Missing_412Stale_200Fresh()
    {
        var client = _fixture.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var (customerId, productId, product2Id) = await SeedAsync(client, ct);
        var orderId = (await CreateOrderAsync(client, customerId, productId, ct)).Id;

        var get = await client.GetAsync($"/api/orders/{orderId}?api-version={ApiVersion}", ct);
        var etag = get.Headers.ETag!;

        // No If-Match → 428 Precondition Required.
        var missing = await client.SendAsync(AddLineItem(orderId, product2Id, ifMatch: null), ct);
        missing.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);

        // Stale If-Match → 412 Precondition Failed.
        var stale = await client.SendAsync(AddLineItem(orderId, product2Id, new EntityTagHeaderValue("\"stale-etag\"")), ct);
        stale.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);

        // Fresh If-Match → 200, and the response carries a new ETag.
        var fresh = await client.SendAsync(AddLineItem(orderId, product2Id, etag), ct);
        fresh.StatusCode.Should().Be(HttpStatusCode.OK);
        fresh.Headers.ETag.Should().NotBeNull();
        fresh.Headers.ETag!.Tag.ToString().Should().NotBe(etag.Tag.ToString());
    }

    [Fact]
    public async Task AddLineItem_SameIdempotencyKey_IsReplayed_NotAppliedTwice()
    {
        var client = _fixture.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var (customerId, productId, product2Id) = await SeedAsync(client, ct);
        var orderId = (await CreateOrderAsync(client, customerId, productId, ct)).Id;

        var get = await client.GetAsync($"/api/orders/{orderId}?api-version={ApiVersion}", ct);
        var etag = get.Headers.ETag!;
        var key = Guid.NewGuid().ToString("N");

        var first = await client.SendAsync(AddLineItem(orderId, product2Id, etag, key), ct);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        // Same key + same body → the middleware replays the cached response without re-running the
        // handler, so the (now-stale) ETag is not re-checked and the product is not added twice.
        var replay = await client.SendAsync(AddLineItem(orderId, product2Id, etag, key), ct);
        replay.StatusCode.Should().Be(HttpStatusCode.OK);

        var final = await client.GetFromJsonAsync<OrderResponse>($"/api/orders/{orderId}?api-version={ApiVersion}", ct);
        final!.LineItems.Count(li => li.ProductId == product2Id).Should().Be(1);
    }

    private static HttpRequestMessage AddLineItem(Guid orderId, Guid productId, EntityTagHeaderValue? ifMatch, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/orders/{orderId}/line-items?api-version={ApiVersion}")
        {
            Content = JsonContent.Create(new { productId, quantity = 1 }),
        };
        if (ifMatch is not null)
            request.Headers.IfMatch.Add(ifMatch);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey ?? Guid.NewGuid().ToString("N"));
        return request;
    }

    private static async Task<(Guid CustomerId, Guid ProductId, Guid Product2Id)> SeedAsync(HttpClient client, CancellationToken ct)
    {
        var customer = await PostAsync<CustomerResponse>(client, $"/api/customers?api-version={ApiVersion}", new
        {
            firstName = "Ada",
            lastName = "Lovelace",
            email = $"etag-{Guid.NewGuid():N}@example.com",
            shippingAddress = new { street = "1 Compute Way", city = "Palo Alto", state = "CA", postalCode = "94301", country = "USA" },
        }, ct);

        var product = await CreateProductAsync(client, ct);
        var product2 = await CreateProductAsync(client, ct);
        return (customer.Id, product.Id, product2.Id);
    }

    private static async Task<ProductResponse> CreateProductAsync(HttpClient client, CancellationToken ct)
    {
        var product = await PostAsync<ProductResponse>(client, $"/api/products?api-version={ApiVersion}",
            new { productName = "Widget", sku = "SKU" + Random.Shared.Next(100000, 999999), unitPrice = 9.99 }, ct);
        (await client.PostAsJsonAsync($"/api/products/{product.Id}/stock-additions?api-version={ApiVersion}", new { quantity = 100 }, ct))
            .EnsureSuccessStatusCode();
        return product;
    }

    private static async Task<OrderResponse> CreateOrderAsync(HttpClient client, Guid customerId, Guid productId, CancellationToken ct) =>
        await PostAsync<OrderResponse>(client, $"/api/orders?api-version={ApiVersion}",
            new { customerId, lineItems = new[] { new { productId, quantity = 1 } } }, ct);

    private static async Task<T> PostAsync<T>(HttpClient client, string url, object body, CancellationToken ct)
    {
        var response = await client.PostAsJsonAsync(url, body, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(ct))!;
    }
}
