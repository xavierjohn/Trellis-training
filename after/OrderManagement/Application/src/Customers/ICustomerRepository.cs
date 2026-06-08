namespace OrderManagement.Application.Customers;

using OrderManagement.Domain;
using Trellis.Primitives;

/// <summary>
/// Repository interface for <see cref="Customer"/> persistence.
/// <para>
/// Mirrors the public surface of <c>RepositoryBase&lt;Customer, CustomerId&gt;</c> from
/// <c>Trellis.EntityFrameworkCore</c> so handlers stage changes here and
/// <c>TransactionalCommandBehavior</c> commits exactly once on success.
/// </para>
/// </summary>
public interface ICustomerRepository
{
    /// <summary>Finds a customer by id. Returns <see cref="Maybe{T}.None"/> if not found.</summary>
    Task<Maybe<Customer>> FindByIdAsync(CustomerId id, CancellationToken cancellationToken);

    /// <summary>
    /// Returns <c>true</c> when a customer with the given email already exists.
    /// Used to surface a deterministic 409 Conflict before EF's unique index would otherwise
    /// raise a wrapped <c>DbUpdateException</c> at <c>SaveChangesAsync</c> time.
    /// </summary>
    Task<bool> ExistsByEmailAsync(EmailAddress email, CancellationToken cancellationToken);

    /// <summary>Stages an aggregate for insertion. The unit-of-work commits on handler success.</summary>
    void Add(Customer customer);
}
