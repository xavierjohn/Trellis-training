namespace Application.Tests;

using OrderManagement.Application;
using OrderManagement.Domain;
using Trellis.Testing.Fakes;

internal class FakeOrderRepositoryAdapter : IOrderRepository
{
    private readonly FakeRepository<Order, OrderId> _repo;

    public FakeOrderRepositoryAdapter(FakeRepository<Order, OrderId> repo) => _repo = repo;

    public async Task<Maybe<Order>> FindByIdAsync(OrderId id, CancellationToken cancellationToken)
    {
        var result = await _repo.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? Maybe.From(result.Value) : Maybe<Order>.None;
    }

    public Task<IReadOnlyList<Order>> FindByCustomerIdAsync(CustomerId customerId, CancellationToken cancellationToken)
    {
        var items = _repo.GetAll().Where(o => o.CustomerId == customerId).ToList();
        return Task.FromResult<IReadOnlyList<Order>>(items);
    }

    public Task<IReadOnlyList<Order>> FindAllAsync(Specification<Order> specification, CancellationToken cancellationToken)
    {
        var items = _repo.GetAll().Where(specification.IsSatisfiedBy).ToList();
        return Task.FromResult<IReadOnlyList<Order>>(items);
    }

    public async Task<Result<Unit>> SaveAsync(Order order, CancellationToken cancellationToken)
    {
        var result = await _repo.SaveAsync(order, cancellationToken);
        return result.Map(_ => default(Unit));
    }
}
