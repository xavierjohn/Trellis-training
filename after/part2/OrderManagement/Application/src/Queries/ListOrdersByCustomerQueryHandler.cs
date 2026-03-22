namespace OrderManagement.Application.Queries;

using Mediator;
using OrderManagement.Application.Abstractions;

public sealed class ListOrdersByCustomerQueryHandler(IOrderRepository orderRepository)
    : IQueryHandler<ListOrdersByCustomerQuery, Result<List<Order>>>
{
    public async ValueTask<Result<List<Order>>> Handle(ListOrdersByCustomerQuery query, CancellationToken cancellationToken) =>
        await orderRepository.GetByCustomerIdAsync(query.CustomerId, cancellationToken);
}
