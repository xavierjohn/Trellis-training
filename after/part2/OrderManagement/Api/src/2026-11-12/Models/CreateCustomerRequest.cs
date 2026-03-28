namespace OrderManagement.Api.v2026_11_12.Models;

using OrderManagement.Domain;
using Trellis.Primitives;

/// <summary>
/// Request model for creating a customer.
/// Value object fields are deserialized directly via Trellis.Asp scalar value binders.
/// </summary>
public record CreateCustomerRequest
{
    /// <summary>Customer first name.</summary>
    public CustomerFirstName FirstName { get; init; } = null!;
    /// <summary>Customer last name.</summary>
    public CustomerLastName LastName { get; init; } = null!;
    /// <summary>Customer email address.</summary>
    public EmailAddress Email { get; init; } = null!;
    /// <summary>Optional phone number.</summary>
    public Maybe<PhoneNumber> PhoneNumber { get; init; }
    /// <summary>Shipping address. All fields required.</summary>
    public ShippingAddressDto ShippingAddress { get; init; } = null!;
}
