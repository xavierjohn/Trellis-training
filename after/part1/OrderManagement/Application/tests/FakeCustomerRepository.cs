#pragma warning disable TRLS003
namespace Application.Tests;

using OrderManagement.Application;
using OrderManagement.Domain;
using Trellis.Testing.Fakes;

public sealed class FakeCustomerRepository : ICustomerRepository
{
    private readonly FakeRepository<Customer, CustomerId> _inner = new();

    public async Task<Maybe<Customer>> FindByIdAsync(CustomerId id, CancellationToken cancellationToken)
    {
        var result = await _inner.FindByIdAsync(id, cancellationToken);
        return result.Value;
    }

    public Task<Result<Unit>> SaveAsync(Customer customer, CancellationToken cancellationToken) =>
        _inner.SaveAsync(customer, cancellationToken);
}
