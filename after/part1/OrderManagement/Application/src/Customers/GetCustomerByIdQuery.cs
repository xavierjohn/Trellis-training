namespace OrderManagement.Application.Customers;

using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>
/// Gets a customer by ID.
/// </summary>
public sealed record GetCustomerByIdQuery(CustomerId CustomerId) : IQuery<Result<Customer>>, IAuthorize
{
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.CustomersRead];
}

/// <summary>
/// Handler for GetCustomerByIdQuery.
/// </summary>
public sealed class GetCustomerByIdQueryHandler : IQueryHandler<GetCustomerByIdQuery, Result<Customer>>
{
    private readonly ICustomerRepository _repository;

    public GetCustomerByIdQueryHandler(ICustomerRepository repository) => _repository = repository;

    public async ValueTask<Result<Customer>> Handle(GetCustomerByIdQuery query, CancellationToken cancellationToken) =>
        (await _repository.FindByIdAsync(query.CustomerId, cancellationToken))
            .ToResult(Error.NotFound($"Customer {query.CustomerId} not found."));
}
