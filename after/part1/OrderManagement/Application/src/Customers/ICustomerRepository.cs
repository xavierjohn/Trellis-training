namespace OrderManagement.Application;

using OrderManagement.Domain;

/// <summary>Repository interface for Customer persistence.</summary>
public interface ICustomerRepository
{
    /// <summary>Finds a customer by ID. Returns Maybe.None if not found.</summary>
    Task<Maybe<Customer>> FindByIdAsync(CustomerId id, CancellationToken cancellationToken);

    /// <summary>Saves a new or updated customer. Returns ConflictError on duplicate email.</summary>
    Task<Result<Unit>> SaveAsync(Customer customer, CancellationToken cancellationToken);
}
