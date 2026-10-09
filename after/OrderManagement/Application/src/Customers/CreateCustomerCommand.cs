namespace OrderManagement.Application.Customers;

using FluentValidation;
using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;
using Trellis.Primitives;

/// <summary>Creates a new customer.</summary>
public sealed record CreateCustomerCommand(
    FirstName FirstName,
    LastName LastName,
    EmailAddress Email,
    Maybe<PhoneNumber> PhoneNumber,
    ShippingAddress ShippingAddress) : ICommand<Result<Customer>>, IAuthorize
{
    /// <inheritdoc />
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.CustomersCreate];
}

public sealed class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(c => c.FirstName).NotNull();
        RuleFor(c => c.LastName).NotNull();
        RuleFor(c => c.Email).NotNull();
        RuleFor(c => c.ShippingAddress).NotNull();
    }
}

public sealed class CreateCustomerCommandHandler(ICustomerRepository repository)
    : ICommandHandler<CreateCustomerCommand, Result<Customer>>
{
    public async ValueTask<Result<Customer>> Handle(CreateCustomerCommand command, CancellationToken cancellationToken)
    {
        if (await repository.ExistsByEmailAsync(command.Email, cancellationToken).ConfigureAwait(false))
            return Result.Fail<Customer>(Error.Conflict.For<Customer>(
                "customer.duplicate-email", id: command.Email.Value,
                detail: $"A customer with email '{command.Email.Value}' already exists."));

        var customer = new Customer(
            command.FirstName,
            command.LastName,
            command.Email,
            command.PhoneNumber,
            command.ShippingAddress);
        repository.Add(customer);
        return Result.Ok(customer);
    }
}
