namespace OrderManagement.AntiCorruptionLayer;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderManagement.Domain;
using Trellis.EntityFrameworkCore;

/// <summary>EF Core configuration for the <see cref="Customer"/> aggregate.</summary>
internal class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(c => c.LastName).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Email).IsRequired().HasMaxLength(320);

        // Embed ShippingAddress directly as owned columns on the Customers row.
        builder.OwnsOne(c => c.ShippingAddress, addr =>
        {
            addr.Property(a => a.Street).IsRequired().HasMaxLength(200);
            addr.Property(a => a.City).IsRequired().HasMaxLength(100);
            addr.Property(a => a.State).IsRequired().HasMaxLength(100);
            addr.Property(a => a.PostalCode).IsRequired().HasMaxLength(20);
            addr.Property(a => a.Country).IsRequired().HasMaxLength(100);
        });

        // Spec §3.1: email is unique across all customers.
        builder.HasIndex(c => c.Email).IsUnique();
    }
}