namespace OrderManagement.AntiCorruptionLayer;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderManagement.Domain;
using Trellis.EntityFrameworkCore;

/// <summary>
/// EF Core configuration for the Order aggregate and LineItem entity.
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

        builder.HasIndex(o => o.CustomerId);
        builder.HasTrellisIndex(o => new { o.Status, o.SubmittedAt });

        builder.OwnsMany(o => o.LineItems, li =>
        {
            li.HasKey(l => l.Id);
            li.Property(l => l.ProductId).IsRequired();
            li.Property(l => l.ProductName).IsRequired();
            li.Property(l => l.Quantity).IsRequired();
            li.WithOwner().HasForeignKey("OrderId");
        });

        builder.Ignore(o => o.Total);
    }
}
