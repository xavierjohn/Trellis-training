namespace OrderManagement.AntiCorruptionLayer;

using Microsoft.EntityFrameworkCore;
using OrderManagement.Application.Customers;
using OrderManagement.Domain;
using Trellis.EntityFrameworkCore;

/// <summary>
/// EF Core implementation of ICustomerRepository.
/// </summary>
internal class CustomerRepository : ICustomerRepository
{
    private readonly AppDbContext _context;

    public CustomerRepository(AppDbContext context) => _context = context;

    public async Task<Maybe<Customer>> FindByIdAsync(CustomerId id, CancellationToken cancellationToken)
    {
        var entity = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return Maybe.From(entity);
    }

    public async Task<Result<Unit>> SaveAsync(Customer customer, CancellationToken cancellationToken)
    {
        var entry = _context.Entry(customer);
        if (entry.State == EntityState.Detached)
            _context.Customers.Add(customer);

        return await _context.SaveChangesResultUnitAsync(cancellationToken).ConfigureAwait(false);
    }
}
