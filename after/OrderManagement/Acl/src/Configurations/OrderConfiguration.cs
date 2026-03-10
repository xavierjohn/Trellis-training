namespace OrderManagement.AntiCorruptionLayer.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderManagement.Domain.Aggregates;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.Id);

        builder.Property(o => o.CustomerId).IsRequired();
        builder.Property(o => o.CreatedByActorId).IsRequired();
        builder.Property(o => o.Status).IsRequired();
        builder.Property(o => o.CreatedAt).IsRequired();

        builder.HasIndex(o => o.CustomerId);
        builder.HasIndex("Status", "_submittedAt").HasDatabaseName("IX_Orders_Status_SubmittedAt");

        builder.HasMany(o => o.LineItems)
            .WithOne()
            .HasForeignKey("OrderId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(o => o.LineItems).AutoInclude();

        builder.Ignore(o => o.IsChanged);
    }
}
