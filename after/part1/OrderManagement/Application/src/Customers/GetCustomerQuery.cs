namespace OrderManagement.Application.Customers;

using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;

public sealed record GetCustomerQuery(CustomerId CustomerId) : IQuery<Result<Customer>>, IAuthorize
{
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.CustomersRead];
}

public sealed class GetCustomerQueryHandler : IQueryHandler<GetCustomerQuery, Result<Customer>>
{
    private readonly ICustomerRepository _repository;

    public GetCustomerQueryHandler(ICustomerRepository repository) => _repository = repository;

    public async ValueTask<Result<Customer>> Handle(GetCustomerQuery query, CancellationToken cancellationToken) =>
        (await _repository.FindByIdAsync(query.CustomerId, cancellationToken))
            .ToResult(Error.NotFound($"Customer {query.CustomerId.Value} not found."));
}
