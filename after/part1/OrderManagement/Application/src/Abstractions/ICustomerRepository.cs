namespace OrderManagement.Application.Abstractions;

public interface ICustomerRepository
{
    Task<Result<Customer>> GetByIdAsync(CustomerId id, CancellationToken cancellationToken = default);
    Task<Result<Maybe<Customer>>> FindByEmailAsync(EmailAddress email, CancellationToken cancellationToken = default);
    Task<Result<Unit>> SaveAsync(Customer customer, CancellationToken cancellationToken = default);
}
