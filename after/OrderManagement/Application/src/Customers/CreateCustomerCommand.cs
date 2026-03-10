namespace OrderManagement.Application.Customers;

using Mediator;
using OrderManagement.Domain.Aggregates;
using OrderManagement.Domain.ValueObjects;
using Trellis.Authorization;
using Trellis.Mediator;
using Trellis.Primitives;

public sealed record CreateCustomerCommand(
    CustomerFirstName FirstName,
    CustomerLastName LastName,
    EmailAddress Email,
    Maybe<PhoneNumber> PhoneNumber,
    ShippingAddress ShippingAddress) : ICommand<Result<Customer>>, IAuthorize
{
    public IReadOnlyList<string> RequiredPermissions => [Domain.Permissions.CustomersCreate];
}
