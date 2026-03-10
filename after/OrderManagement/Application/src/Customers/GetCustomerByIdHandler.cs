namespace OrderManagement.Application.Customers;

using Mediator;
using OrderManagement.Application.Repositories;
using OrderManagement.Domain.Aggregates;

public sealed class GetCustomerByIdHandler(ICustomerRepository customerRepository)
    : IQueryHandler<GetCustomerByIdQuery, Result<Customer>>
{
    public async ValueTask<Result<Customer>> Handle(GetCustomerByIdQuery query, CancellationToken cancellationToken) =>
        await customerRepository.GetByIdAsync(query.CustomerId, cancellationToken);
}
