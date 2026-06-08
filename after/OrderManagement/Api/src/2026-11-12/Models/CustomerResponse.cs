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
        Street = address.Street.Value,
        City = address.City.Value,
        State = address.State.Value,
        PostalCode = address.PostalCode.Value,
        Country = address.Country.Value,
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
        Id = customer.Id.Value,
        FirstName = customer.FirstName.Value,
        LastName = customer.LastName.Value,
        Email = customer.Email.Value,
        PhoneNumber = customer.PhoneNumber.Match<string?>(p => p.Value, () => null),
        ShippingAddress = ShippingAddressResponse.From(customer.ShippingAddress),
    };
}
