namespace OrderManagement.AntiCorruptionLayer.Repositories;

using Microsoft.EntityFrameworkCore;
using OrderManagement.Application.Abstractions;
using Trellis.EntityFrameworkCore;
using Trellis.Primitives;

public class CustomerRepository(AppDbContext dbContext) : ICustomerRepository
{
    public async Task<Result<Customer>> GetByIdAsync(CustomerId id, CancellationToken cancellationToken = default) =>
        await dbContext.Customers
            .FirstOrDefaultResultAsync(c => c.Id == id, Error.NotFound($"Customer '{id}' not found"), cancellationToken);

    public async Task<Result<Maybe<Customer>>> FindByEmailAsync(EmailAddress email, CancellationToken cancellationToken = default) =>
        Result.Success(await dbContext.Customers
            .FirstOrDefaultMaybeAsync(c => c.Email == email, cancellationToken));

    public async Task<Result<Unit>> SaveAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        if (dbContext.Entry(customer).State == EntityState.Detached)
            dbContext.Customers.Add(customer);
        else
            dbContext.Customers.Update(customer);
        return await dbContext.SaveChangesResultUnitAsync(cancellationToken);
    }
}
