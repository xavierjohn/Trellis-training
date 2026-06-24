namespace VanillaCorrect;

using Microsoft.EntityFrameworkCore;

// Same anemic POCO models as the defective arm, with ONE addition that does the heavy lifting for
// correctness: Product.Version, an explicit optimistic-concurrency token. Everything else that makes
// this arm correct lives in the request handlers (Program.cs), by hand.

public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int Stock { get; set; }
    public decimal Price { get; set; }

    /// <summary>Concurrency token, bumped on every stock change so a concurrent reservation conflicts.</summary>
    public Guid Version { get; set; }
}

public class LineItem
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}

public class Order
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTime? SubmittedAt { get; set; }
    public List<LineItem> Items { get; set; } = [];
}

public class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<LineItem> LineItems => Set<LineItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        // The token must be hand-declared and hand-bumped; without it the read-modify-write oversells.
        modelBuilder.Entity<Product>().Property(p => p.Version).IsConcurrencyToken();
}
