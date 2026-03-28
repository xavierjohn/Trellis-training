namespace OrderManagement.Domain;

/// <summary>
/// Shipping address value object with street, city, state, postal code, and country.
/// </summary>
public class ShippingAddress : ValueObject
{
    public Street Street { get; }
    public City City { get; }
    public State State { get; }
    public PostalCode PostalCode { get; }
    public Country Country { get; }

    private ShippingAddress(Street street, City city, State state, PostalCode postalCode, Country country)
    {
        Street = street;
        City = city;
        State = state;
        PostalCode = postalCode;
        Country = country;
    }

    public static Result<ShippingAddress> TryCreate(
        Street street, City city, State state, PostalCode postalCode, Country country) =>
        new ShippingAddress(street, city, state, postalCode, country);

    /// <summary>EF Core constructor.</summary>
    private ShippingAddress()
    {
        Street = null!;
        City = null!;
        State = null!;
        PostalCode = null!;
        Country = null!;
    }

    protected override IEnumerable<IComparable?> GetEqualityComponents()
    {
        yield return Street.Value;
        yield return City.Value;
        yield return State.Value;
        yield return PostalCode.Value;
        yield return Country.Value;
    }
}
