namespace OrderManagement.Application.Customers;

using OrderManagement.Domain;

/// <summary>
/// Repository interface for Customer persistence.
/// </summary>
public interface ICustomerRepository
{
    Task<Maybe<Customer>> FindByIdAsync(CustomerId id, CancellationToken cancellationToken);
    Task<Result<Unit>> SaveAsync(Customer customer, CancellationToken cancellationToken);
}
