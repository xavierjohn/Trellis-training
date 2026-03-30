namespace OrderManagement.AntiCorruptionLayer;

using Microsoft.EntityFrameworkCore;
using OrderManagement.Application;
using OrderManagement.Domain;
using Trellis.EntityFrameworkCore;

internal class CustomerRepository : ICustomerRepository
{
    private readonly AppDbContext _context;

    public CustomerRepository(AppDbContext context) => _context = context;

    public async Task<Maybe<Customer>> FindByIdAsync(CustomerId id, CancellationToken cancellationToken) =>
        await _context.Customers.FirstOrDefaultMaybeAsync(c => c.Id == id, cancellationToken);

    public async Task<Result<Unit>> SaveAsync(Customer customer, CancellationToken cancellationToken)
    {
        var entry = _context.Entry(customer);
        if (entry.State == EntityState.Detached)
            _context.Customers.Add(customer);

        return await _context.SaveChangesResultUnitAsync(cancellationToken);
    }
}
