namespace OrderManagement.Application;

using OrderManagement.Domain;

public interface ICustomerRepository
{
    Task<Maybe<Customer>> FindByIdAsync(CustomerId id, CancellationToken cancellationToken);
    Task<Result<Unit>> SaveAsync(Customer customer, CancellationToken cancellationToken);
}
