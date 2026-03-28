namespace OrderManagement.Api.v2026_11_12.Models;

using OrderManagement.Domain;
using Trellis.Primitives;

/// <summary>
/// Request model for creating a customer.
/// </summary>
public record CreateCustomerRequest
{
    /// <summary>Customer first name.</summary>
    public FirstName FirstName { get; init; } = null!;

    /// <summary>Customer last name.</summary>
    public LastName LastName { get; init; } = null!;

    /// <summary>Customer email address.</summary>
    public EmailAddress Email { get; init; } = null!;

    /// <summary>Customer phone number, if provided.</summary>
    public Maybe<PhoneNumber> PhoneNumber { get; init; }

    /// <summary>Customer shipping address.</summary>
    public ShippingAddressRequest ShippingAddress { get; init; } = null!;
}

/// <summary>
/// Shipping address fields for the create customer request.
/// </summary>
public record ShippingAddressRequest
{
    /// <summary>Street address.</summary>
    public Street Street { get; init; } = null!;

    /// <summary>City name.</summary>
    public City City { get; init; } = null!;

    /// <summary>State or province.</summary>
    public State State { get; init; } = null!;

    /// <summary>Postal or ZIP code.</summary>
    public PostalCode PostalCode { get; init; } = null!;

    /// <summary>Country name.</summary>
    public Country Country { get; init; } = null!;
}
