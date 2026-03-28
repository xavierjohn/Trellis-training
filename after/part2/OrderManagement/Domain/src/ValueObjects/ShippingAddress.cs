namespace OrderManagement.Domain;

/// <summary>
/// A shipping address value object with all required fields.
/// </summary>
public sealed class ShippingAddress : ValueObject
{
    public string Street { get; }
    public string City { get; }
    public string State { get; }
    public string PostalCode { get; }
    public string Country { get; }

    private ShippingAddress(string street, string city, string state, string postalCode, string country)
    {
        Street = street;
        City = city;
        State = state;
        PostalCode = postalCode;
        Country = country;
    }

    /// <summary>Creates a shipping address, validating all required fields are non-empty.</summary>
    public static Result<ShippingAddress> TryCreate(
        string? street, string? city, string? state, string? postalCode, string? country)
    {
        var errors = new List<(string field, string message)>();

        if (string.IsNullOrWhiteSpace(street)) errors.Add(("street", "Street is required."));
        if (string.IsNullOrWhiteSpace(city)) errors.Add(("city", "City is required."));
        if (string.IsNullOrWhiteSpace(state)) errors.Add(("state", "State is required."));
        if (string.IsNullOrWhiteSpace(postalCode)) errors.Add(("postalCode", "Postal code is required."));
        if (string.IsNullOrWhiteSpace(country)) errors.Add(("country", "Country is required."));

        if (errors.Count > 0)
        {
            var first = errors[0];
            var error = ValidationError.For(first.field, first.message);
            for (var i = 1; i < errors.Count; i++)
                error = error.And(errors[i].field, errors[i].message);
            return error;
        }

        return new ShippingAddress(street!.Trim(), city!.Trim(), state!.Trim(), postalCode!.Trim(), country!.Trim());
    }

    /// <inheritdoc />
    protected override IEnumerable<IComparable?> GetEqualityComponents()
    {
        yield return Street;
        yield return City;
        yield return State;
        yield return PostalCode;
        yield return Country;
    }
}
