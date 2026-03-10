namespace OrderManagement.AntiCorruptionLayer.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderManagement.Domain.Aggregates;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.FirstName).IsRequired();
        builder.Property(c => c.LastName).IsRequired();
        builder.Property(c => c.Email).IsRequired();

        builder.HasIndex(c => c.Email).IsUnique();

        builder.OwnsOne(c => c.ShippingAddress, sa =>
        {
            sa.Property(a => a.Street).IsRequired().HasColumnName("ShippingStreet");
            sa.Property(a => a.City).IsRequired().HasColumnName("ShippingCity");
            sa.Property(a => a.State).IsRequired().HasColumnName("ShippingState");
            sa.Property(a => a.PostalCode).IsRequired().HasColumnName("ShippingPostalCode");
            sa.Property(a => a.Country).IsRequired().HasColumnName("ShippingCountry");
        });

        builder.Ignore(c => c.IsChanged);
    }
}
