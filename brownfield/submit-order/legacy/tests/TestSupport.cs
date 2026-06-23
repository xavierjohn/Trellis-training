namespace Legacy.Tests;

using Legacy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

/// <summary>Seeds products and draft orders into a legacy <see cref="AppDb"/>.</summary>
internal static class Seed
{
    // Monotonic line-item ids so EF's "ORDER BY LineItem.Id" Include returns items in insertion
    // order — makes the per-item-save corruption deterministic regardless of GUID sort.
    private static int _lineItemSequence;

    public static Guid Product(TestDb db, string name, int stock, decimal price)
    {
        using var ctx = db.NewContext();
        return Product(ctx, name, stock, price);
    }

    public static Guid Product(AppDb ctx, string name, int stock, decimal price)
    {
        var product = new Product { Id = Guid.NewGuid(), Name = name, Stock = stock, Price = price };
        ctx.Products.Add(product);
        ctx.SaveChanges();
        return product.Id;
    }

    public static Guid Order(TestDb db, params (Guid ProductId, int Quantity)[] items)
    {
        using var ctx = db.NewContext();
        return Order(ctx, items);
    }

    public static Guid Order(AppDb ctx, params (Guid ProductId, int Quantity)[] items)
    {
        var order = new Order { Id = Guid.NewGuid(), CustomerId = Guid.NewGuid(), Status = "Draft" };
        foreach (var (productId, quantity) in items)
            order.Items.Add(new LineItem
            {
                Id = Guid.Parse($"{Interlocked.Increment(ref _lineItemSequence):D8}-0000-0000-0000-000000000000"),
                OrderId = order.Id,
                ProductId = productId,
                Quantity = quantity,
                UnitPrice = 1m,
            });
        ctx.Orders.Add(order);
        ctx.SaveChanges();
        return order.Id;
    }
}

/// <summary>An isolated in-memory SQLite database (one open connection) for direct-EF tests.</summary>
internal sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDb> _options;

    public TestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDb>().UseSqlite(_connection).Options;
        using var ctx = NewContext();
        ctx.Database.EnsureCreated();
    }

    public AppDb NewContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}

/// <summary>Boots the real legacy HTTP app over a throwaway SQLite file for integration tests.</summary>
internal sealed class LegacyApp : IAsyncDisposable
{
    private readonly string _databasePath;
    private readonly DbContextOptions<AppDb> _options;

    public WebApplicationFactory<Program> Factory { get; }

    public LegacyApp()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"legacy-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={_databasePath}";
        _options = new DbContextOptionsBuilder<AppDb>().UseSqlite(connectionString).Options;
        using (var ctx = new AppDb(_options))
            ctx.Database.EnsureCreated();

        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Db", connectionString));
    }

    public Guid SeedProduct(string name, int stock, decimal price)
    {
        using var ctx = new AppDb(_options);
        return Seed.Product(ctx, name, stock, price);
    }

    public Guid SeedOrder(Guid productId, int qty)
    {
        using var ctx = new AppDb(_options);
        return Seed.Order(ctx, (productId, qty));
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        try { File.Delete(_databasePath); } catch { /* best-effort temp cleanup */ }
    }
}
