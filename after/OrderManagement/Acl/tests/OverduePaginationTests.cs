namespace AntiCorruptionLayer.Tests;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using OrderManagement.AntiCorruptionLayer;
using OrderManagement.Domain;
using Trellis.Authorization;
using Trellis.EntityFrameworkCore;
using Trellis.Primitives;

/// <summary>
/// Verifies the overdue-orders query pages correctly on the real SQLite provider: the
/// <see cref="OverdueOrderSpecification"/> filter (a <c>DateTimeOffset</c> comparison, only
/// translatable via the Acl's UTC ISO-8601 value converter) composes with the forward-only
/// id-seek cursor and over-fetch so a multi-page walk returns every overdue order exactly once.
/// </summary>
public class OverduePaginationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public OverduePaginationTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .AddTrellisInterceptors()
            .Options;

        using var db = new AppDbContext(_options);
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task OverdueQuery_PagesThroughCursor_NoDuplicatesOrGaps()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

        // Seed five submitted orders, all submitted at the same (old) instant.
        var created = new HashSet<Guid>();
        using (var seed = new AppDbContext(_options))
        {
            for (var i = 0; i < 5; i++)
            {
                var product = new Product(
                    ProductName.Create("Widget"),
                    Sku.Create($"SKU{i:D5}"),
                    UnitPrice.Create(1m));
                product.AddStock(10).IsSuccess.Should().BeTrue();

                var order = new Order(CustomerId.NewUniqueV7(), ActorId.Create("actor-1"), clock);
                order.AddLineItem(product.Id, product.ProductName, LineItemQuantity.Create(1), product.UnitPrice)
                    .IsSuccess.Should().BeTrue();
                order.Submit(new Dictionary<ProductId, Product> { [product.Id] = product }, clock)
                    .IsSuccess.Should().BeTrue();

                seed.Orders.Add(order);
                created.Add(order.Id.Value);
            }

            (await seed.SaveChangesResultAsync(ct)).IsSuccess.Should().BeTrue();
        }

        // Query 30 days later, so every order (submitted 7+ days ago) is overdue.
        var asOf = new DateTimeOffset(2026, 1, 31, 12, 0, 0, TimeSpan.Zero);

        using var read = new AppDbContext(_options);
        var repository = new OrderRepository(read);

        var seen = new List<Guid>();
        Cursor? cursor = null;
        var pages = 0;
        bool sawNextOnFirstPage = false;

        do
        {
            var result = await repository.QueryPageAsync(
                new OverdueOrderSpecification(asOf), PageSize.FromRequested(2), cursor, ct);

            result.TryGetValue(out var page, out var error).Should().BeTrue(error?.ToString());
            page.AppliedLimit.Should().Be(2);
            page.Items.Count.Should().BeLessThanOrEqualTo(2);

            if (pages == 0)
                sawNextOnFirstPage = page.Next is not null;

            seen.AddRange(page.Items.Select(o => o.Id.Value));
            cursor = page.Next;
            pages++;
        }
        while (cursor is not null && pages < 10);

        sawNextOnFirstPage.Should().BeTrue("5 overdue orders at 2 per page must yield a next cursor");
        pages.Should().Be(3, "5 orders paged by 2 spans three pages (2 + 2 + 1)");
        seen.Should().HaveCount(5);
        seen.Should().OnlyHaveUniqueItems();
        seen.Should().BeEquivalentTo(created);
    }
}
