namespace OrderManagement.Application.Queries;

using Mediator;
using OrderManagement.Application.Abstractions;

public sealed class GetOrderByIdQueryHandler(IOrderRepository orderRepository)
    : IQueryHandler<GetOrderByIdQuery, Result<Order>>
{
    public async ValueTask<Result<Order>> Handle(GetOrderByIdQuery query, CancellationToken cancellationToken) =>
        await orderRepository.GetByIdAsync(query.OrderId, cancellationToken);
}
