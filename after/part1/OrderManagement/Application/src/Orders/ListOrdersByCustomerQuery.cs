namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;

public sealed record ListOrdersByCustomerQuery(CustomerId CustomerId) : IQuery<Result<IReadOnlyList<Order>>>, IAuthorize
{
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersReadAll];
}

public sealed class ListOrdersByCustomerQueryHandler : IQueryHandler<ListOrdersByCustomerQuery, Result<IReadOnlyList<Order>>>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IOrderRepository _orderRepository;

    public ListOrdersByCustomerQueryHandler(ICustomerRepository customerRepository, IOrderRepository orderRepository)
    {
        _customerRepository = customerRepository;
        _orderRepository = orderRepository;
    }

    public async ValueTask<Result<IReadOnlyList<Order>>> Handle(ListOrdersByCustomerQuery query, CancellationToken cancellationToken)
    {
        var customerResult = (await _customerRepository.FindByIdAsync(query.CustomerId, cancellationToken))
            .ToResult(Error.NotFound($"Customer {query.CustomerId.Value} not found."));
        if (customerResult.IsFailure)
            return customerResult.Error;

        var orders = await _orderRepository.GetByCustomerIdAsync(query.CustomerId, cancellationToken);
        return Result.Success<IReadOnlyList<Order>>(orders);
    }
}
