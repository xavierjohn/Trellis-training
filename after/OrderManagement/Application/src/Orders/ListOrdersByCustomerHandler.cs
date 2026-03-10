namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Application.Repositories;
using OrderManagement.Domain.Aggregates;

public sealed class ListOrdersByCustomerHandler(
    ICustomerRepository customerRepository,
    IOrderRepository orderRepository)
    : IQueryHandler<ListOrdersByCustomerQuery, Result<List<Order>>>
{
    public async ValueTask<Result<List<Order>>> Handle(ListOrdersByCustomerQuery query, CancellationToken cancellationToken) =>
        await customerRepository.GetByIdAsync(query.CustomerId, cancellationToken)
            .BindAsync((Func<Customer, Task<Result<List<Order>>>>)(
                async _ => await orderRepository.GetByCustomerIdAsync(query.CustomerId, cancellationToken)));
}
