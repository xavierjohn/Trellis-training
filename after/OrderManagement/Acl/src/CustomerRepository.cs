namespace OrderManagement.AntiCorruptionLayer;

using Microsoft.EntityFrameworkCore;
using OrderManagement.Application.Customers;
using OrderManagement.Domain;
using Trellis.EntityFrameworkCore;
using Trellis.Primitives;

/// <summary>EF Core implementation of <see cref="ICustomerRepository"/>.</summary>
internal sealed class CustomerRepository : RepositoryBase<Customer, CustomerId>, ICustomerRepository
{
    public CustomerRepository(AppDbContext context) : base(context) { }

    public Task<bool> ExistsByEmailAsync(EmailAddress email, CancellationToken cancellationToken) =>
        DbSet.AnyAsync(c => c.Email == email, cancellationToken);
}