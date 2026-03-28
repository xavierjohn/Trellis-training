namespace OrderManagement.AntiCorruptionLayer;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderManagement.Domain;

/// <summary>
/// EF Core configuration for the Customer aggregate.
/// </summary>
internal class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.FirstName).IsRequired();
        builder.Property(c => c.LastName).IsRequired();
        builder.Property(c => c.Email).IsRequired();

        builder.HasIndex(c => c.Email).IsUnique();

        // PhoneNumber is Maybe<PhoneNumber> — handled by ApplyTrellisConventions

        builder.OwnsOne(c => c.ShippingAddress, sa =>
        {
            sa.Property(a => a.Street).HasColumnName("ShippingAddress_Street").IsRequired().HasMaxLength(500);
            sa.Property(a => a.City).HasColumnName("ShippingAddress_City").IsRequired().HasMaxLength(200);
            sa.Property(a => a.State).HasColumnName("ShippingAddress_State").IsRequired().HasMaxLength(200);
            sa.Property(a => a.PostalCode).HasColumnName("ShippingAddress_PostalCode").IsRequired().HasMaxLength(20);
            sa.Property(a => a.Country).HasColumnName("ShippingAddress_Country").IsRequired().HasMaxLength(200);
        });
    }
}
