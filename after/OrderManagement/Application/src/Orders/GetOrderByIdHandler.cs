namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Application.Repositories;
using OrderManagement.Domain.Aggregates;

public sealed class GetOrderByIdHandler(IOrderRepository orderRepository)
    : IQueryHandler<GetOrderByIdQuery, Result<Order>>
{
    public async ValueTask<Result<Order>> Handle(GetOrderByIdQuery query, CancellationToken cancellationToken) =>
        await orderRepository.GetByIdAsync(query.OrderId, cancellationToken);
}
