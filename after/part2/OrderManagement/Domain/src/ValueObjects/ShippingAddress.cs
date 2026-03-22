namespace OrderManagement.Domain;

public class ShippingAddress : ValueObject
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

    public static Result<ShippingAddress> TryCreate(
        string? street,
        string? city,
        string? state,
        string? postalCode,
        string? country)
    {
        Error? error = null;

        if (string.IsNullOrWhiteSpace(street))
            error = error.Combine(Error.Validation("Street is required", "street"));
        if (string.IsNullOrWhiteSpace(city))
            error = error.Combine(Error.Validation("City is required", "city"));
        if (string.IsNullOrWhiteSpace(state))
            error = error.Combine(Error.Validation("State is required", "state"));
        if (string.IsNullOrWhiteSpace(postalCode))
            error = error.Combine(Error.Validation("PostalCode is required", "postalCode"));
        if (string.IsNullOrWhiteSpace(country))
            error = error.Combine(Error.Validation("Country is required", "country"));

        if (error is not null)
            return error;

        return new ShippingAddress(
            street!.Trim(),
            city!.Trim(),
            state!.Trim(),
            postalCode!.Trim(),
            country!.Trim());
    }

    protected override IEnumerable<IComparable?> GetEqualityComponents()
    {
        yield return Street;
        yield return City;
        yield return State;
        yield return PostalCode;
        yield return Country;
    }
}
