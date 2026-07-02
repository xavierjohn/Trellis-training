namespace OrderManagement.Api.v2026_11_12.Models;

using OrderManagement.Domain;

/// <summary>Shipping address nested inside a customer response.</summary>
public record ShippingAddressResponse
{
    public string Street { get; init; } = null!;
    public string City { get; init; } = null!;
    public string State { get; init; } = null!;
    public string PostalCode { get; init; } = null!;
    public string Country { get; init; } = null!;

    public static ShippingAddressResponse From(ShippingAddress address) => new()
    {
        Street = address.Street,
        City = address.City,
        State = address.State,
        PostalCode = address.PostalCode,
        Country = address.Country,
    };
}

/// <summary>Customer response model.</summary>
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
        Id = customer.Id,
        FirstName = customer.FirstName,
        LastName = customer.LastName,
        Email = customer.Email,
        PhoneNumber = customer.PhoneNumber.Match<string?>(p => p.Value, () => null),
        ShippingAddress = ShippingAddressResponse.From(customer.ShippingAddress),
    };
}
