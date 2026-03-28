namespace OrderManagement.AntiCorruptionLayer;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderManagement.Domain;

/// <summary>
/// EF Core configuration for the Product aggregate.
/// </summary>
internal class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.ProductName).IsRequired();
        builder.Property(p => p.Sku).IsRequired();
        builder.Property(p => p.StockQuantity).IsRequired();

        builder.HasIndex(p => p.Sku).IsUnique();

        // UnitPrice (Money) is automatically mapped by MoneyConvention via ApplyTrellisConventions.
        // Columns: UnitPrice (decimal 18,3) and UnitPriceCurrency (nvarchar 3).
    }
}
