namespace OrderManagement.Domain;

/// <summary>
/// A composite value object representing a shipping address.
/// </summary>
public class ShippingAddress : ValueObject
{
    public Street Street { get; }
    public City City { get; }
    public State State { get; }
    public PostalCode PostalCode { get; }
    public Country Country { get; }

    private ShippingAddress() // EF Core
    {
        Street = null!;
        City = null!;
        State = null!;
        PostalCode = null!;
        Country = null!;
    }

    private ShippingAddress(Street street, City city, State state, PostalCode postalCode, Country country)
    {
        Street = street;
        City = city;
        State = state;
        PostalCode = postalCode;
        Country = country;
    }

    public static Result<ShippingAddress> TryCreate(Street street, City city, State state, PostalCode postalCode, Country country) =>
        new ShippingAddress(street, city, state, postalCode, country);

    protected override IEnumerable<IComparable?> GetEqualityComponents()
    {
        yield return Street;
        yield return City;
        yield return State;
        yield return PostalCode;
        yield return Country;
    }
}
