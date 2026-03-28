namespace Application.Tests;

using OrderManagement.Application;
using OrderManagement.Domain;
using Trellis.Testing.Fakes;

internal class FakeCustomerRepositoryAdapter : ICustomerRepository
{
    private readonly FakeRepository<Customer, CustomerId> _repo;

    public FakeCustomerRepositoryAdapter(FakeRepository<Customer, CustomerId> repo) => _repo = repo;

    public async Task<Maybe<Customer>> FindByIdAsync(CustomerId id, CancellationToken cancellationToken)
    {
        var result = await _repo.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? Maybe.From(result.Value) : Maybe<Customer>.None;
    }

    public async Task<Result<Unit>> SaveAsync(Customer customer, CancellationToken cancellationToken)
    {
        var result = await _repo.SaveAsync(customer, cancellationToken);
        return result.Map(_ => default(Unit));
    }
}
