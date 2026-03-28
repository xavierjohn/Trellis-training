namespace Application.Tests;

using OrderManagement.Application.Orders;
using OrderManagement.Domain;
using Trellis.Authorization;
using Trellis.Testing.Fakes;

internal class FakeCancelOrderResourceLoader : ResourceLoaderById<CancelOrderCommand, Order, OrderId>
{
    private readonly FakeRepository<Order, OrderId> _repo;

    public FakeCancelOrderResourceLoader(FakeRepository<Order, OrderId> repo) => _repo = repo;

    protected override OrderId GetId(CancelOrderCommand message) => message.OrderId;

    protected override Task<Result<Order>> GetByIdAsync(OrderId id, CancellationToken cancellationToken) =>
        _repo.GetByIdAsync(id, cancellationToken);
}
