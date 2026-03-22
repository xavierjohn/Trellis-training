namespace OrderManagement.Application.Commands;

using Mediator;
using OrderManagement.Application.Abstractions;

public sealed class CreateCustomerCommandHandler(ICustomerRepository customerRepository)
    : ICommandHandler<CreateCustomerCommand, Result<Customer>>
{
    public async ValueTask<Result<Customer>> Handle(CreateCustomerCommand command, CancellationToken cancellationToken) =>
        await Customer.TryCreate(
                command.FirstName,
                command.LastName,
                command.Email,
                command.PhoneNumber,
                command.ShippingAddress)
            .TapAsync(customer => customerRepository.SaveAsync(customer, cancellationToken));
}
