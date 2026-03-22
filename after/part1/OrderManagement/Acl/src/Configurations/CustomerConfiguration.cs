namespace OrderManagement.AntiCorruptionLayer.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.EntityFrameworkCore;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.HasKey(c => c.Id);
        builder.HasIndex(c => c.Email).IsUnique();

        builder.OwnsOne(c => c.ShippingAddress, sa =>
        {
            sa.Property(x => x.Street).HasColumnName("Street").IsRequired();
            sa.Property(x => x.City).HasColumnName("City").IsRequired();
            sa.Property(x => x.State).HasColumnName("State").IsRequired();
            sa.Property(x => x.PostalCode).HasColumnName("PostalCode").IsRequired();
            sa.Property(x => x.Country).HasColumnName("Country").IsRequired();
        });
    }
}
