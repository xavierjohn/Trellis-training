#pragma warning disable CS1591
namespace OrderManagement.Api.v2026_11_12.Models;

using OrderManagement.Domain;
using Trellis.Primitives;

public record CreateCustomerRequest
{
    public FirstName FirstName { get; init; } = null!;
    public LastName LastName { get; init; } = null!;
    public EmailAddress Email { get; init; } = null!;
    public Maybe<PhoneNumber> PhoneNumber { get; init; }
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

public record CustomerResponse
{
    public Guid Id { get; init; }
    public string FirstName { get; init; } = null!;
    public string LastName { get; init; } = null!;
    public string Email { get; init; } = null!;
    public string? PhoneNumber { get; init; }
    public ShippingAddressResponse ShippingAddress { get; init; } = null!;

    public static CustomerResponse From(Customer customer) => new()
    {
        Id = customer.Id.Value,
        FirstName = customer.FirstName.Value,
        LastName = customer.LastName.Value,
        Email = customer.Email.Value,
        PhoneNumber = customer.PhoneNumber.Match<string?>(p => p.Value, () => null),
        ShippingAddress = new ShippingAddressResponse
        {
            Street = customer.ShippingAddress.Street.Value,
            City = customer.ShippingAddress.City.Value,
            State = customer.ShippingAddress.State.Value,
            PostalCode = customer.ShippingAddress.PostalCode.Value,
            Country = customer.ShippingAddress.Country.Value
        }
    };
}

public record ShippingAddressResponse
{
    public string Street { get; init; } = null!;
    public string City { get; init; } = null!;
    public string State { get; init; } = null!;
    public string PostalCode { get; init; } = null!;
    public string Country { get; init; } = null!;
}
