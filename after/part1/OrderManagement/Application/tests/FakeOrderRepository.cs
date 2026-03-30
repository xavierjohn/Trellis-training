#pragma warning disable TRLS003
namespace Application.Tests;

using OrderManagement.Application;
using OrderManagement.Domain;
using Trellis.Testing.Fakes;

public sealed class FakeOrderRepository : IOrderRepository
{
    private readonly FakeRepository<Order, OrderId> _inner = new();

    public async Task<Maybe<Order>> FindByIdAsync(OrderId id, CancellationToken cancellationToken)
    {
        var result = await _inner.FindByIdAsync(id, cancellationToken);
        return result.Value;
    }

    public Task<List<Order>> GetByCustomerIdAsync(CustomerId customerId, CancellationToken cancellationToken)
    {
        var orders = _inner.GetAll().Where(o => o.CustomerId == customerId).ToList();
        return Task.FromResult(orders);
    }

    public Task<List<Order>> GetOverdueAsync(DateTime cutoff, CancellationToken cancellationToken)
    {
        var spec = new OverdueOrderSpecification(cutoff);
        var overdue = _inner.GetAll().Where(spec.IsSatisfiedBy).ToList();
        return Task.FromResult(overdue);
    }

    public Task<Result<Unit>> SaveAsync(Order order, CancellationToken cancellationToken) =>
        _inner.SaveAsync(order, cancellationToken);
}
