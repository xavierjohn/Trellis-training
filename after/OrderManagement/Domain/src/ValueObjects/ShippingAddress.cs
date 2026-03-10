namespace OrderManagement.Domain.ValueObjects;

public class ShippingAddress : ValueObject
{
    public Street Street { get; private set; } = null!;
    public City City { get; private set; } = null!;
    public State State { get; private set; } = null!;
    public PostalCode PostalCode { get; private set; } = null!;
    public Country Country { get; private set; } = null!;

    private ShippingAddress() { }

    public static Result<ShippingAddress> TryCreate(
        Street street,
        City city,
        State state,
        PostalCode postalCode,
        Country country)
    {
        return new ShippingAddress
        {
            Street = street,
            City = city,
            State = state,
            PostalCode = postalCode,
            Country = country
        };
    }

    protected override IEnumerable<IComparable> GetEqualityComponents()
    {
        yield return Street.Value;
        yield return City.Value;
        yield return State.Value;
        yield return PostalCode.Value;
        yield return Country.Value;
    }
}
