namespace Legacy.Tests;

using System.Net;
using Legacy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

/// <summary>
/// Each test reproduces a real defect in the legacy Submit-Order endpoint. A passing run means the
/// bug fires. The Trellis "after" (../../README.md) makes every one of these structurally
/// impossible — see after/OrderManagement/Domain/src/Aggregates/Order.cs and the SubmitOrderCommand.
/// </summary>
public class SubmitOrderBugTests
{
    // Sanity: the README promises `dotnet run` yields a working service. This boots the real app on
    // a brand-new database that nothing pre-creates, exercising the startup EnsureCreated. Without
    // it, the first request 500s with a missing-table error instead of a clean 404.
    [Fact]
    public async Task App_BootsAndCreatesSchema_OnAFreshDatabase()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"legacy-fresh-{Guid.NewGuid():N}.db");
        try
        {
            await using var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:Db", $"Data Source={dbPath}"));
            var client = factory.CreateClient();

            var health = await client.GetAsync("/health", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, health.StatusCode);

            // A submit against the freshly-created (empty) schema returns 404 — not a 500 missing-table crash.
            var submit = await client.PostAsync($"/orders/{Guid.NewGuid()}/submit", content: null, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, submit.StatusCode);
        }
        finally
        {
            try { File.Delete(dbPath); } catch { /* best-effort temp cleanup */ }
        }
    }

    // BUG 1 — Concurrency / lost update → oversell.
    [Fact]
    public void Bug1_NoOptimisticConcurrency_ConcurrentSubmits_Oversell()
    {
        using var h = new TestDb();
        var productId = Seed.Product(h, "Widget", stock: 5, price: 10m);
        var orderA = Seed.Order(h, (productId, 5));
        var orderB = Seed.Order(h, (productId, 5));

        // Two concurrent requests each get their own DbContext and BOTH read the product before
        // either writes — exactly the interleave SubmitAsync's read-modify-write produces under
        // load. (Sequential SubmitAsync calls cannot show this: the 2nd reload sees the 1st commit.)
        using var ctxA = h.NewContext();
        using var ctxB = h.NewContext();
        var pA = ctxA.Products.Single(p => p.Id == productId); // reads stock = 5
        var pB = ctxB.Products.Single(p => p.Id == productId); // reads stock = 5 (stale snapshot)

        pA.Stock -= 5;
        ctxA.Orders.Single(o => o.Id == orderA).Status = "Submitted";
        ctxA.SaveChanges();

        pB.Stock -= 5;
        ctxB.Orders.Single(o => o.Id == orderB).Status = "Submitted";
        ctxB.SaveChanges(); // no DbUpdateConcurrencyException — the stale write silently wins

        using var verify = h.NewContext();
        // 5 units existed; two orders each "reserved" 5 → 10 units sold, both Submitted.
        Assert.Equal(0, verify.Products.Single(p => p.Id == productId).Stock);
        Assert.Equal(2, verify.Orders.Count(o => o.Status == "Submitted"));
        // A Trellis aggregate carries an ETag/row-version, so ctxB.SaveChanges() would throw a
        // concurrency conflict and force B to retry against the fresh stock of 0.
    }

    // BUG 2 — Atomicity: a later line item fails after earlier items were already persisted.
    [Fact]
    public async Task Bug2_PerItemSave_LeavesStockReservedForAnOrderThatNeverSubmits()
    {
        using var h = new TestDb();
        var inStockA = Seed.Product(h, "InStock-A", stock: 100, price: 1m);
        var inStockB = Seed.Product(h, "InStock-B", stock: 100, price: 1m);
        var outOfStock = Seed.Product(h, "OutOfStock", stock: 0, price: 1m);
        var order = Seed.Order(h, (inStockA, 10), (inStockB, 10), (outOfStock, 5));

        using (var ctx = h.NewContext())
            await Assert.ThrowsAnyAsync<Exception>(() => SubmitOrderEndpoint.SubmitAsync(ctx, order, ["*"]));

        using var verify = h.NewContext();
        var stockA = verify.Products.Single(p => p.Id == inStockA).Stock;
        var stockB = verify.Products.Single(p => p.Id == inStockB).Stock;
        // At least one in-stock line item was decremented and SAVED before the out-of-stock item
        // failed — the per-item save persisted a partial reservation. (Monotonic line-item ids keep
        // the out-of-stock item last; this assertion does not depend on exactly which items ran.)
        Assert.True(stockA < 100 || stockB < 100, "expected a persisted partial reservation");
        Assert.Equal(0, verify.Products.Single(p => p.Id == outOfStock).Stock);
        // ...yet the order never reached Submitted. Stock is reserved for an order still in Draft.
        Assert.Equal("Draft", verify.Orders.Single(o => o.Id == order).Status);
        // Trellis Order.Submit pre-flights every reservation BEFORE mutating any product, so a
        // shortfall on any item leaves all stock untouched ("no partial reservations leak through").
    }

    // BUG 4 — No state guard: re-submitting reserves stock a second time.
    [Fact]
    public async Task Bug4_NoStateGuard_ResubmittingReservesStockTwice()
    {
        using var h = new TestDb();
        var productId = Seed.Product(h, "Widget", stock: 10, price: 10m);
        var order = Seed.Order(h, (productId, 5));

        using (var ctx = h.NewContext())
            await SubmitOrderEndpoint.SubmitAsync(ctx, order, ["*"]); // 10 -> 5, Submitted
        using (var ctx = h.NewContext())
            await SubmitOrderEndpoint.SubmitAsync(ctx, order, ["*"]); // no guard → reserves AGAIN, 5 -> 0

        using var verify = h.NewContext();
        // A 5-unit order consumed 10 units of stock. Trellis' LazyStateMachine permits Submit only
        // from Draft, so the 2nd call returns a 422 before any stock moves.
        Assert.Equal(0, verify.Products.Single(p => p.Id == productId).Stock);
        Assert.Equal("Submitted", verify.Orders.Single(o => o.Id == order).Status);
    }

    // BUG 3 — Error mapping: a business failure surfaces as 500 + leaked internal detail.
    [Fact]
    public async Task Bug3_InsufficientStock_Returns500WithLeakedMessage_NotA4xx()
    {
        await using var app = new LegacyApp();
        var productId = app.SeedProduct("Widget", stock: 1, price: 10m);
        var order = app.SeedOrder(productId, qty: 5); // wants 5, only 1 available
        var client = app.Factory.CreateClient();

        var response = await client.PostAsync($"/orders/{order}/submit", content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode); // a rejected business rule should be a 4xx, not a 500
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Insufficient stock", body); // internal detail leaked to the caller
        // Trellis maps Error.InvalidInput.ForRule(...) to a 4xx client error + RFC 9457 ProblemDetails
        // (the OM spec's error table lists 400; Trellis defaults to 422) — never a 500, no stack trace.
    }

    // BUG 5 — Authorization: the orders:submit permission is never checked.
    [Fact]
    public async Task Bug5_NoAuthorizationCheck_ActorWithoutPermission_StillSubmits()
    {
        await using var app = new LegacyApp();
        var productId = app.SeedProduct("Widget", stock: 100, price: 10m);
        var order = app.SeedOrder(productId, qty: 5);
        var client = app.Factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, $"/orders/{order}/submit");
        request.Headers.Add("X-Test-Actor", "{\"Id\":\"intruder\",\"Permissions\":[\"orders:read\"]}");
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // should be 403 Forbidden
        // Trellis' SubmitOrderCommand : IAuthorize with RequiredPermissions = [orders:submit] is
        // enforced by the mediator pipeline before the handler runs → 403.
    }
}
