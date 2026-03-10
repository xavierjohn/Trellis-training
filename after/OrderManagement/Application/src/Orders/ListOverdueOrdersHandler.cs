namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Application.Repositories;
using OrderManagement.Domain.Aggregates;

public sealed class ListOverdueOrdersHandler(IOrderRepository orderRepository)
    : IQueryHandler<ListOverdueOrdersQuery, Result<List<Order>>>
{
    public async ValueTask<Result<List<Order>>> Handle(ListOverdueOrdersQuery query, CancellationToken cancellationToken) =>
        await orderRepository.GetOverdueOrdersAsync(DateTime.UtcNow, cancellationToken);
}
