namespace OrderManagement.Domain;

/// <summary>
/// Composite shipping address. All five components are required.
///
/// NOT decorated with <c>[OwnedEntity]</c> here because that attribute lives in
/// <c>Trellis.EntityFrameworkCore</c> and Domain must not reference the EF layer.
/// EF ownership is configured in the ACL layer's <c>CustomerConfiguration</c> via
/// <c>.OwnsOne&lt;ShippingAddress&gt;(...)</c>.
/// </summary>
public partial class ShippingAddress : ValueObject
{
    public Street Street { get; private set; }
    public City City { get; private set; }
    public StateRegion State { get; private set; }
    public PostalCode PostalCode { get; private set; }
    public Country Country { get; private set; }

    /// <summary>EF Core constructor.</summary>
    private ShippingAddress()
    {
        Street = null!;
        City = null!;
        State = null!;
        PostalCode = null!;
        Country = null!;
    }

    public ShippingAddress(Street street, City city, StateRegion state, PostalCode postalCode, Country country)
    {
        Street = street;
        City = city;
        State = state;
        PostalCode = postalCode;
        Country = country;
    }

    public static ShippingAddress Create(Street street, City city, StateRegion state, PostalCode postalCode, Country country) =>
        new(street, city, state, postalCode, country);

    protected override IEnumerable<IComparable?> GetEqualityComponents()
    {
        yield return Street;
        yield return City;
        yield return State;
        yield return PostalCode;
        yield return Country;
    }
}
