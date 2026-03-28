namespace OrderManagement.Api.v2026_11_12.Models;

/// <summary>
/// Shipping address shape used in both request and response DTOs.
/// </summary>
public record ShippingAddressDto
{
    /// <summary>Street address line.</summary>
    public string Street { get; init; } = null!;
    /// <summary>City name.</summary>
    public string City { get; init; } = null!;
    /// <summary>State or province.</summary>
    public string State { get; init; } = null!;
    /// <summary>Postal or ZIP code.</summary>
    public string PostalCode { get; init; } = null!;
    /// <summary>Country name or code.</summary>
    public string Country { get; init; } = null!;
}
