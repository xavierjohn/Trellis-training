namespace OrderManagement.Api.v2026_11_12.Models;

using OrderManagement.Domain.Aggregates;
using OrderManagement.Domain.ValueObjects;
using Trellis.Primitives;

public record CustomerResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    ShippingAddressResponse ShippingAddress)
{
    public static CustomerResponse From(Customer customer) => new(
        customer.Id.Value,
        customer.FirstName.Value,
        customer.LastName.Value,
        customer.Email.Value,
        customer.PhoneNumber.Match(p => (string?)p.Value, () => null),
        ShippingAddressResponse.From(customer.ShippingAddress));
}

public record ShippingAddressResponse(
    string Street,
    string City,
    string State,
    string PostalCode,
    string Country)
{
    public static ShippingAddressResponse From(ShippingAddress address) => new(
        address.Street.Value,
        address.City.Value,
        address.State.Value,
        address.PostalCode.Value,
        address.Country.Value);
}

public record CreateCustomerRequest
{
    public CustomerFirstName FirstName { get; init; } = null!;
    public CustomerLastName LastName { get; init; } = null!;
    public EmailAddress Email { get; init; } = null!;
    public PhoneNumber? PhoneNumber { get; init; }
    public ShippingAddressRequest ShippingAddress { get; init; } = null!;
}

public record ShippingAddressRequest
{
    public Street Street { get; init; } = null!;
    public City City { get; init; } = null!;
    public State State { get; init; } = null!;
    public PostalCode PostalCode { get; init; } = null!;
    public Country Country { get; init; } = null!;
}
