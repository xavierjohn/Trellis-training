namespace OrderManagement.AntiCorruptionLayer;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderManagement.Domain;
using Trellis.EntityFrameworkCore;

/// <summary>
/// EF Core configuration for the Order aggregate.
/// </summary>
internal class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.Id);

        builder.Property(o => o.CustomerId).IsRequired();
        builder.Property(o => o.CreatedByActorId).IsRequired().HasMaxLength(200);
        builder.Property(o => o.Status).IsRequired().HasConversion<string>();
        builder.Property(o => o.CreatedAt).IsRequired();

        // SubmittedAt and ShippedAt are partial Maybe<DateTime> — handled by ApplyTrellisConventions

        builder.HasIndex(o => o.CustomerId);
        builder.HasTrellisIndex(o => new { o.Status, o.SubmittedAt });

        // Use the backing field for the IReadOnlyList<LineItem> navigation
        builder.Navigation(o => o.LineItems)
            .HasField("_lineItems")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Use string-based overload to avoid type-inference limitations with IReadOnlyList<LineItem>
        builder.OwnsMany<LineItem>(nameof(Order.LineItems), li =>
        {
            li.WithOwner().HasForeignKey("OrderId");
            li.HasKey(l => l.Id);

            li.Property(l => l.ProductId).IsRequired();
            li.Property(l => l.ProductName).IsRequired();
            li.Property(l => l.Quantity).IsRequired();

            // UnitPrice (Money) inside LineItem is automatically mapped by MoneyConvention.
            // Columns: UnitPrice (decimal 18,3) and UnitPriceCurrency (nvarchar 3).
        });
    }
}
