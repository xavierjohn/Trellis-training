namespace OrderManagement.AntiCorruptionLayer;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderManagement.Domain;

/// <summary>EF Core configuration for the <see cref="Product"/> aggregate.</summary>
internal class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.ProductName).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Sku).IsRequired().HasMaxLength(20);
        builder.Property(p => p.UnitPrice).IsRequired();
        builder.Property(p => p.StockQuantity).IsRequired();

        // Spec §3.2: SKU is unique across all products.
        builder.HasIndex(p => p.Sku).IsUnique();
    }
}