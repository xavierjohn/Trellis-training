namespace Vanilla;

using Microsoft.EntityFrameworkCore;

// Anemic models: raw primitives, no value objects, no invariants, Status is a magic string, and
// there is no optimistic-concurrency token to detect a lost update. Nothing here stops Stock from
// going negative or Quantity from being out of range.

public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int Stock { get; set; }
    public decimal Price { get; set; }
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
}
