namespace OrderManagement.Application.Commands;

using Mediator;
using Trellis.Authorization;
using Trellis.Mediator;
using Trellis.Primitives;

public sealed record CreateCustomerCommand(
    FirstName FirstName,
    LastName LastName,
    EmailAddress Email,
    Maybe<PhoneNumber> PhoneNumber,
    ShippingAddress ShippingAddress)
    : ICommand<Result<Customer>>, IAuthorize
{
    public IReadOnlyList<string> RequiredPermissions => [Permissions.CustomersCreate];
}
