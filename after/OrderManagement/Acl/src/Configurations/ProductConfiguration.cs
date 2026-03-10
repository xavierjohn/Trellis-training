namespace OrderManagement.AntiCorruptionLayer.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderManagement.Domain.Aggregates;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.ProductName).IsRequired();
        builder.Property(p => p.Sku).IsRequired();

        builder.HasIndex(p => p.Sku).IsUnique();

        builder.Ignore(p => p.IsChanged);
    }
}
