namespace Application.Tests;

using OrderManagement.Application.Orders;
using OrderManagement.Domain;
using Trellis.Authorization;

public sealed class FakeCancelOrderResourceLoader : IResourceLoader<CancelOrderCommand, Order>
{
    private readonly FakeOrderRepository _repository;

    public FakeCancelOrderResourceLoader(FakeOrderRepository repository) => _repository = repository;

    public async Task<Result<Order>> LoadAsync(CancelOrderCommand message, CancellationToken cancellationToken)
    {
        var maybe = await _repository.FindByIdAsync(message.OrderId, cancellationToken);
        return maybe.ToResult(Error.NotFound($"Order {message.OrderId.Value} not found."));
    }
}
