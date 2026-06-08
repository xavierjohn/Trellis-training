namespace OrderManagement.AntiCorruptionLayer;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderManagement.Domain;
using Trellis.EntityFrameworkCore;

/// <summary>EF Core configuration for the <see cref="Order"/> aggregate (with owned LineItems).</summary>
internal class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.Id);

        builder.Property(o => o.CustomerId).IsRequired();
        builder.Property(o => o.CreatedByActorId).IsRequired().HasMaxLength(200);
        builder.Property(o => o.Status).IsRequired();

        // Status is the common filter on List Overdue Orders and per-customer lists.
        builder.HasTrellisIndex(o => new { o.Status, o.CustomerId });

        // LineItems are an owned collection of the Order aggregate (no separate
        // repository — accessed exclusively through Order).
        builder.OwnsMany(o => o.LineItems, li =>
        {
            li.WithOwner().HasForeignKey("OrderId");
            li.HasKey(x => x.Id);
            li.Property(x => x.ProductId).IsRequired();
            li.Property(x => x.ProductName).IsRequired().HasMaxLength(200);
            li.Property(x => x.Quantity).IsRequired();
            li.Property(x => x.UnitPrice).IsRequired();
        });

        // EF needs to discover the owned-collection backing field for change tracking.
        builder.Metadata.FindNavigation(nameof(Order.LineItems))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}