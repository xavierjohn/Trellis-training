namespace Application.Tests;

using OrderManagement.Application.Orders;
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

    public async Task<Result<Unit>> SaveAsync(Order order, CancellationToken cancellationToken)
    {
        var result = await _repo.SaveAsync(order, cancellationToken);
        return result.Map(_ => default(Unit));
    }

    public Task<IReadOnlyList<Order>> GetByCustomerIdAsync(CustomerId customerId, CancellationToken cancellationToken)
    {
        var orders = _repo.GetAll().Where(o => o.CustomerId == customerId).ToList();
        return Task.FromResult<IReadOnlyList<Order>>(orders);
    }

    public Task<IReadOnlyList<Order>> GetOverdueOrdersAsync(DateTime utcNow, CancellationToken cancellationToken)
    {
        var spec = new OverdueOrderSpecification(utcNow);
        var orders = _repo.GetAll().Where(spec.IsSatisfiedBy).ToList();
        return Task.FromResult<IReadOnlyList<Order>>(orders);
    }
}
