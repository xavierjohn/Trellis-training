namespace OrderManagement.AntiCorruptionLayer.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.EntityFrameworkCore;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.Id);
        builder.HasIndex(o => o.CustomerId);
        builder.HasTrellisIndex(o => new { o.Status, o.SubmittedAt });

        builder.OwnsMany(o => o.LineItems, li =>
        {
            li.WithOwner().HasForeignKey("OrderId");
            li.HasKey(x => x.Id);
            li.Property(x => x.Quantity);
            li.Property(x => x.ProductName);
        });
    }
}
