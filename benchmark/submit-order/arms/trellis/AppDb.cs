namespace TrellisArm;

using Microsoft.EntityFrameworkCore;
using Trellis.EntityFrameworkCore;

/// <summary>
/// EF Core context with Trellis conventions. <see cref="ApplyTrellisConventions"/> maps the value
/// objects, marks owned-collection domain keys non-store-generated, and configures the aggregate
/// ETag as a concurrency token; paired with <c>AddTrellisInterceptors()</c> (in Program.cs) it
/// stamps a fresh ETag on every save, which is what turns a concurrent oversell into a detectable
/// conflict (R1).
/// </summary>
public class AppDb : DbContext
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();

    public AppDb(DbContextOptions<AppDb> options) : base(options) { }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.ApplyTrellisConventions(typeof(ProductId).Assembly);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(b =>
        {
            b.HasKey(p => p.Id);
            b.Property(p => p.Name).IsRequired().HasMaxLength(200);
            b.Property(p => p.Stock).IsRequired();
        });

        modelBuilder.Entity<Order>(b =>
        {
            b.HasKey(o => o.Id);
            b.Property(o => o.CustomerId).IsRequired();
            b.Property(o => o.Status).IsRequired();

            b.OwnsMany(o => o.LineItems, li =>
            {
                li.WithOwner().HasForeignKey("OrderId");
                li.HasKey(x => x.Id);
                li.Property(x => x.ProductId).IsRequired();
                li.Property(x => x.Quantity).IsRequired();
            });

            b.Metadata.FindNavigation(nameof(Order.LineItems))!
                .SetPropertyAccessMode(PropertyAccessMode.Field);
        });
    }
}
