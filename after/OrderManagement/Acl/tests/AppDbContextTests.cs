namespace AntiCorruptionLayer.Tests;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrderManagement.AntiCorruptionLayer;
using OrderManagement.Domain;
using Trellis.EntityFrameworkCore;
using Trellis.Primitives;

/// <summary>
/// Smoke tests for the EF Core model — verifies the model builds, the schema can be
/// created on SQLite (in-memory), and that the canonical write-then-read round-trip
/// preserves value-object identity and owned-collection state.
/// </summary>
public class AppDbContextTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public AppDbContextTests()
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
    public async Task EnsureCreated_BuildsSchema_WithoutError()
    {
        using var db = new AppDbContext(_options);
        var customers = await db.Customers.ToListAsync(TestContext.Current.CancellationToken);
        var products = await db.Products.ToListAsync(TestContext.Current.CancellationToken);
        var orders = await db.Orders.ToListAsync(TestContext.Current.CancellationToken);

        customers.Should().BeEmpty();
        products.Should().BeEmpty();
        orders.Should().BeEmpty();
    }

    [Fact]
    public async Task PersistAndReload_CustomerWithOwnedShippingAddress_RoundTrips()
    {
        var customer = new Customer(
            FirstName.Create("Ada"),
            LastName.Create("Lovelace"),
            EmailAddress.Create("ada@example.com"),
            Maybe<PhoneNumber>.None,
            new ShippingAddress(
                Street.Create("1 Compute Way"),
                City.Create("Mountain View"),
                StateRegion.Create("CA"),
                PostalCode.Create("94043"),
                Country.Create("USA")));

        using (var write = new AppDbContext(_options))
        {
            write.Customers.Add(customer);
            (await write.SaveChangesResultAsync(TestContext.Current.CancellationToken))
                .IsSuccess.Should().BeTrue();
        }

        using var read = new AppDbContext(_options);
        var reloaded = await read.Customers.SingleAsync(c => c.Id == customer.Id, TestContext.Current.CancellationToken);

        reloaded.Email.Value.Should().Be("ada@example.com");
        reloaded.ShippingAddress.City.Value.Should().Be("Mountain View");
    }

    [Fact]
    public async Task UniqueEmailConstraint_IsEnforcedByDatabase()
    {
        var addr = new ShippingAddress(
            Street.Create("1 A St"),
            City.Create("Town"),
            StateRegion.Create("CA"),
            PostalCode.Create("94000"),
            Country.Create("USA"));

        using (var db = new AppDbContext(_options))
        {
            db.Customers.Add(new Customer(
                FirstName.Create("First"),
                LastName.Create("Person"),
                EmailAddress.Create("collide@example.com"),
                Maybe<PhoneNumber>.None,
                addr));
            (await db.SaveChangesResultAsync(TestContext.Current.CancellationToken))
                .IsSuccess.Should().BeTrue();
        }

        using var db2 = new AppDbContext(_options);
        db2.Customers.Add(new Customer(
            FirstName.Create("Second"),
            LastName.Create("Person"),
            EmailAddress.Create("collide@example.com"),
            Maybe<PhoneNumber>.None,
            addr));

        var result = await db2.SaveChangesResultAsync(TestContext.Current.CancellationToken);
        result.IsFailure.Should().BeTrue("unique-index violation must surface as a Result failure rather than throw");
    }
}