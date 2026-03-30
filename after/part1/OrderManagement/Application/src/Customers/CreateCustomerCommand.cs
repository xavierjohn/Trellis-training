namespace OrderManagement.Application.Customers;

using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;
using Trellis.Primitives;

public sealed record CreateCustomerCommand(
    FirstName FirstName,
    LastName LastName,
    EmailAddress Email,
    Maybe<PhoneNumber> PhoneNumber,
    ShippingAddress ShippingAddress) : ICommand<Result<Customer>>, IAuthorize
{
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.CustomersCreate];
}

public sealed class CreateCustomerCommandHandler : ICommandHandler<CreateCustomerCommand, Result<Customer>>
{
    private readonly ICustomerRepository _repository;

    public CreateCustomerCommandHandler(ICustomerRepository repository) => _repository = repository;

    public async ValueTask<Result<Customer>> Handle(CreateCustomerCommand command, CancellationToken cancellationToken) =>
        await Customer.TryCreate(command.FirstName, command.LastName, command.Email, command.PhoneNumber, command.ShippingAddress)
            .CheckAsync(customer => _repository.SaveAsync(customer, cancellationToken));
}
