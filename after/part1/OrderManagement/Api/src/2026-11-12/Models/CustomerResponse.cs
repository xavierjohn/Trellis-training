namespace OrderManagement.Api.v2026_11_12.Models;

using OrderManagement.Domain;

/// <summary>
/// Response model for a customer.
/// </summary>
public record CustomerResponse
{
    /// <summary>Customer identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Customer first name.</summary>
    public string FirstName { get; init; } = null!;

    /// <summary>Customer last name.</summary>
    public string LastName { get; init; } = null!;

    /// <summary>Customer email address.</summary>
    public string Email { get; init; } = null!;

    /// <summary>Customer phone number, if provided.</summary>
    public string? PhoneNumber { get; init; }

    /// <summary>Customer shipping address.</summary>
    public ShippingAddressResponse ShippingAddress { get; init; } = null!;

    /// <summary>Maps from domain model to response.</summary>
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

/// <summary>
/// Response model for a shipping address.
/// </summary>
public record ShippingAddressResponse
{
    /// <summary>Street address.</summary>
    public string Street { get; init; } = null!;

    /// <summary>City name.</summary>
    public string City { get; init; } = null!;

    /// <summary>State or province.</summary>
    public string State { get; init; } = null!;

    /// <summary>Postal or ZIP code.</summary>
    public string PostalCode { get; init; } = null!;

    /// <summary>Country name.</summary>
    public string Country { get; init; } = null!;
}
