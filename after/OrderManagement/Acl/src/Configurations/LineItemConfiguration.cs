namespace OrderManagement.AntiCorruptionLayer.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderManagement.Domain.Aggregates;

public class LineItemConfiguration : IEntityTypeConfiguration<LineItem>
{
    public void Configure(EntityTypeBuilder<LineItem> builder)
    {
        builder.HasKey(li => li.Id);

        builder.Property(li => li.ProductId).IsRequired();
        builder.Property(li => li.ProductName).IsRequired();
        builder.Property(li => li.Quantity).IsRequired();
    }
}
