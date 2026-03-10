namespace OrderManagement.Application.Customers;

using Mediator;
using OrderManagement.Application.Repositories;
using OrderManagement.Domain.Aggregates;

public sealed class CreateCustomerHandler(ICustomerRepository customerRepository)
    : ICommandHandler<CreateCustomerCommand, Result<Customer>>
{
    public async ValueTask<Result<Customer>> Handle(CreateCustomerCommand command, CancellationToken cancellationToken) =>
        await customerRepository.ExistsByEmailAsync(command.Email, cancellationToken)
            .BindAsync(exists => exists
                ? Result.Failure<Trellis.Unit>(Error.Conflict($"A customer with email '{command.Email}' already exists."))
                : Result.Success())
            .BindAsync(_ => Customer.TryCreate(
                command.FirstName,
                command.LastName,
                command.Email,
                command.PhoneNumber,
                command.ShippingAddress))
            .BindAsync((Func<Customer, Task<Result<Customer>>>)(async customer =>
                await customerRepository.SaveAsync(customer, cancellationToken)));
}
