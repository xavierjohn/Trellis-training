namespace OrderManagement.Application.Customers;

using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;
using Trellis.Primitives;

/// <summary>Creates a new customer.</summary>
public sealed record CreateCustomerCommand(
    CustomerFirstName FirstName,
    CustomerLastName LastName,
    EmailAddress Email,
    Maybe<PhoneNumber> PhoneNumber,
    ShippingAddress ShippingAddress) : ICommand<Result<Customer>>, IAuthorize
{
    /// <inheritdoc />
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.CustomersCreate];
}

/// <summary>Handler for <see cref="CreateCustomerCommand"/>.</summary>
public sealed class CreateCustomerCommandHandler : ICommandHandler<CreateCustomerCommand, Result<Customer>>
{
    private readonly ICustomerRepository _repository;

    /// <summary>Initializes a new instance of <see cref="CreateCustomerCommandHandler"/>.</summary>
    public CreateCustomerCommandHandler(ICustomerRepository repository) => _repository = repository;

    /// <inheritdoc />
    public async ValueTask<Result<Customer>> Handle(CreateCustomerCommand command, CancellationToken cancellationToken) =>
        await Customer.TryCreate(
                command.FirstName, command.LastName, command.Email, command.PhoneNumber, command.ShippingAddress)
            .BindAsync(customer => _repository.SaveAsync(customer, cancellationToken).MapAsync(_ => customer));
}
