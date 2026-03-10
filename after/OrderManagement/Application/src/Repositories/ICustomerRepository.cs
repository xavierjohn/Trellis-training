namespace OrderManagement.Application.Repositories;

using OrderManagement.Domain.Aggregates;
using OrderManagement.Domain.ValueObjects;
using Trellis.Primitives;

public interface ICustomerRepository
{
    Task<Result<Customer>> GetByIdAsync(CustomerId id, CancellationToken ct);
    Task<Result<Customer>> SaveAsync(Customer customer, CancellationToken ct);
    Task<Result<bool>> ExistsByEmailAsync(EmailAddress email, CancellationToken ct);
}
