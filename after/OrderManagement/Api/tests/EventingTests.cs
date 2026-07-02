namespace Api.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderManagement.Api.v2026_11_12.Models;
using OrderManagement.AntiCorruptionLayer;
using OrderManagement.Application.IntegrationEvents;
using Trellis.EntityFrameworkCore;
using Trellis.Mediator;

/// <summary>
/// Integration tests for the transactional outbox / idempotent inbox payment round-trip:
/// submitting an order captures an outbox message; a PaymentConfirmed event flows through the
/// inbox exactly once (redeliveries are de-duplicated); and approval is gated until payment
/// is confirmed. Payment is dispatched straight through the inbox for determinism, mirroring
/// what the external payments service would send over the broker.
/// </summary>
[Collection(TestWebApplicationFactoryCollectionFixture.Id)]
public class EventingTests
{
    private const string ApiVersion = "2026-11-12";
    private readonly TestWebApplicationFactoryFixture _fixture;

    public EventingTests(TestWebApplicationFactoryFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Submitting_an_order_captures_an_outbox_message()
    {
        var client = _fixture.CreateClient();

        // The in-memory SQLite database is shared across the collection, so assert on the delta
        // rather than absolute presence: this submit must add exactly one OrderSubmitted outbox
        // row, captured in the same transaction as the order change.
        var before = await CountOrderSubmittedOutboxAsync();
        await CreateAndSubmitOrderAsync(client);
        var after = await CountOrderSubmittedOutboxAsync();

        after.Should().Be(before + 1);
    }

    private async Task<int> CountOrderSubmittedOutboxAsync()
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Set<OutboxMessage>()
            .CountAsync(m => m.EventType.Contains("OrderSubmitted"), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Payment_confirmation_unblocks_approval()
    {
        var client = _fixture.CreateClient();
        var (order, orderTotal) = await CreateAndSubmitOrderAsync(client);

        // Before payment, approval is blocked (422 — the not-paid invariant).
        var blocked = await client.PostAsync(
            $"/api/orders/{order.Id}/approval?api-version={ApiVersion}", EmptyBody(),
            TestContext.Current.CancellationToken);
        blocked.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        await ConfirmPaymentAsync(order.Id, orderTotal);

        var approved = await client.PostAsync(
            $"/api/orders/{order.Id}/approval?api-version={ApiVersion}", EmptyBody(),
            TestContext.Current.CancellationToken);
        approved.StatusCode.Should().Be(HttpStatusCode.OK);

        // The confirmed payment is exposed on the order response.
        var approvedOrder = (await approved.Content.ReadFromJsonAsync<OrderResponse>(TestContext.Current.CancellationToken))!;
        approvedOrder.PaidAt.Should().NotBeNull();
        approvedOrder.PaymentReference.Should().NotBeNull();
    }

    [Fact]
    public async Task Dispatching_the_same_payment_event_twice_is_deduplicated()
    {
        var client = _fixture.CreateClient();
        var (order, orderTotal) = await CreateAndSubmitOrderAsync(client);

        var inbox = _fixture.Services.GetRequiredService<IInboxDispatcher>();
        var paymentEvent = new PaymentConfirmedIntegrationEvent(
            Guid.NewGuid(), order.Id, orderTotal, $"PAY-{order.Id:N}", DateTimeOffset.UtcNow, "USD");
        var envelope = new IntegrationEnvelope(paymentEvent.EventId, paymentEvent) { MessageSource = "payments" };

        var first = await inbox.DispatchAsync(envelope, TestContext.Current.CancellationToken);
        var second = await inbox.DispatchAsync(envelope, TestContext.Current.CancellationToken);

        first.Should().Be(InboxDispatchOutcome.Processed);
        second.Should().Be(InboxDispatchOutcome.SkippedDuplicate);
    }

    private async Task ConfirmPaymentAsync(Guid orderId, decimal amountPaid)
    {
        var inbox = _fixture.Services.GetRequiredService<IInboxDispatcher>();
        var paymentEvent = new PaymentConfirmedIntegrationEvent(
            Guid.NewGuid(), orderId, amountPaid, $"PAY-{orderId:N}", DateTimeOffset.UtcNow, "USD");
        var envelope = new IntegrationEnvelope(paymentEvent.EventId, paymentEvent) { MessageSource = "payments" };
        await inbox.DispatchAsync(envelope, TestContext.Current.CancellationToken);
    }

    private static async Task<(OrderResponse order, decimal orderTotal)> CreateAndSubmitOrderAsync(HttpClient client)
    {
        var customer = await CreateAsync<CustomerResponse>(client, $"/api/customers?api-version={ApiVersion}", new
        {
            FirstName = "Grace",
            LastName = "Hopper",
            Email = $"grace-{Guid.NewGuid():N}@example.com",
            ShippingAddress = new { Street = "1 Main St", City = "Redmond", State = "WA", PostalCode = "98052", Country = "USA" },
        });

        var product = await CreateAsync<ProductResponse>(client, $"/api/products?api-version={ApiVersion}", new
        {
            ProductName = "Gadget",
            Sku = $"SKU{Random.Shared.Next(100000, 999999)}",
            UnitPrice = 5m,
        });
        await CreateAsync<ProductResponse>(client, $"/api/products/{product.Id}/stock-additions?api-version={ApiVersion}", new { Quantity = 10 });

        var order = await CreateAsync<OrderResponse>(client, $"/api/orders?api-version={ApiVersion}", new
        {
            CustomerId = customer.Id,
            LineItems = new[] { new { ProductId = product.Id, Quantity = 1 } },
        });

        var submitResponse = await client.PostAsync(
            $"/api/orders/{order.Id}/submission?api-version={ApiVersion}", EmptyBody(),
            TestContext.Current.CancellationToken);
        submitResponse.EnsureSuccessStatusCode();
        var submitted = (await submitResponse.Content.ReadFromJsonAsync<OrderResponse>(TestContext.Current.CancellationToken))!;

        return (submitted, submitted.OrderTotal);
    }

    private static async Task<T> CreateAsync<T>(HttpClient client, string url, object payload)
    {
        var response = await client.PostAsJsonAsync(url, payload, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(TestContext.Current.CancellationToken))!;
    }

    private static StringContent EmptyBody() => new("", Encoding.UTF8, "application/json");
}
