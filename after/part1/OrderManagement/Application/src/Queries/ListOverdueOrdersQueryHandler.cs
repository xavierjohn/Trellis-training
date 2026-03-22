namespace OrderManagement.Application.Queries;

using Mediator;
using OrderManagement.Application.Abstractions;

public sealed class ListOverdueOrdersQueryHandler(IOrderRepository orderRepository)
    : IQueryHandler<ListOverdueOrdersQuery, Result<List<Order>>>
{
    public async ValueTask<Result<List<Order>>> Handle(ListOverdueOrdersQuery query, CancellationToken cancellationToken) =>
        await orderRepository.GetOverdueOrdersAsync(cancellationToken);
}
