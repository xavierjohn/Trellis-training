namespace OrderManagement.Api.v2026_11_12.Models;

using OrderManagement.Domain;

/// <summary>
/// Response model for a customer.
/// </summary>
public record CustomerResponse
{
    /// <summary>Unique customer identifier.</summary>
    public Guid Id { get; init; }
    /// <summary>Customer first name.</summary>
    public string FirstName { get; init; } = null!;
    /// <summary>Customer last name.</summary>
    public string LastName { get; init; } = null!;
    /// <summary>Customer email address.</summary>
    public string Email { get; init; } = null!;
    /// <summary>Optional phone number.</summary>
    public string? PhoneNumber { get; init; }
    /// <summary>Default shipping address.</summary>
    public ShippingAddressDto ShippingAddress { get; init; } = null!;

    /// <summary>Maps from domain aggregate.</summary>
    public static CustomerResponse From(Customer c) => new()
    {
        Id = c.Id.Value,
        FirstName = c.FirstName.Value,
        LastName = c.LastName.Value,
        Email = c.Email.Value,
        PhoneNumber = c.PhoneNumber.Match<string?>(p => p.Value, () => null),
        ShippingAddress = new ShippingAddressDto
        {
            Street = c.ShippingAddress.Street,
            City = c.ShippingAddress.City,
            State = c.ShippingAddress.State,
            PostalCode = c.ShippingAddress.PostalCode,
            Country = c.ShippingAddress.Country
        }
    };
}
