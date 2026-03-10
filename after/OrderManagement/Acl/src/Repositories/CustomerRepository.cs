namespace OrderManagement.AntiCorruptionLayer.Repositories;

using Microsoft.EntityFrameworkCore;
using OrderManagement.Application.Repositories;
using OrderManagement.Domain.Aggregates;
using OrderManagement.Domain.ValueObjects;
using Trellis.EntityFrameworkCore;
using Trellis.Primitives;

public class CustomerRepository(OrderManagementDbContext dbContext) : ICustomerRepository
{
    public async Task<Result<Customer>> GetByIdAsync(CustomerId id, CancellationToken ct) =>
        await dbContext.Customers
            .FirstOrDefaultResultAsync(c => c.Id == id, Error.NotFound($"Customer '{id}' not found."), ct);

    public async Task<Result<Customer>> SaveAsync(Customer customer, CancellationToken ct)
    {
        var entry = dbContext.Entry(customer);
        if (entry.State == EntityState.Detached)
        {
            dbContext.Customers.Add(customer);
        }

        return await dbContext.SaveChangesResultUnitAsync(ct)
            .MapAsync(_ => customer);
    }

    public async Task<Result<bool>> ExistsByEmailAsync(EmailAddress email, CancellationToken ct) =>
        Result.Success(await dbContext.Customers.AnyAsync(c => c.Email == email, ct));
}
