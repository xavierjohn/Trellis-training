namespace OrderManagement.Api.v2026_11_12.Models;

using OrderManagement.Domain;
using Trellis.Primitives;

/// <summary>Request model for creating a customer.</summary>
public record CreateCustomerRequest
{
    public FirstName FirstName { get; init; } = null!;
    public LastName LastName { get; init; } = null!;
    public EmailAddress Email { get; init; } = null!;
    public Maybe<PhoneNumber> PhoneNumber { get; init; }
    public CreateShippingAddressRequest ShippingAddress { get; init; } = null!;
}

/// <summary>Shipping address nested inside <see cref="CreateCustomerRequest"/>.</summary>
public record CreateShippingAddressRequest
{
    public Street Street { get; init; } = null!;
    public City City { get; init; } = null!;
    public StateRegion State { get; init; } = null!;
    public PostalCode PostalCode { get; init; } = null!;
    public Country Country { get; init; } = null!;

    public ShippingAddress ToDomain() => new(Street, City, State, PostalCode, Country);
}
