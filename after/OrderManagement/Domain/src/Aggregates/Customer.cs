namespace OrderManagement.Domain;

using Trellis.Primitives;

/// <summary>
/// A person or organization that places orders. Identified by <see cref="CustomerId"/>.
/// </summary>
public partial class Customer : Aggregate<CustomerId>
{
    public FirstName FirstName { get; private set; } = null!;
    public LastName LastName { get; private set; } = null!;
    public EmailAddress Email { get; private set; } = null!;

    /// <summary>Optional phone number. Stored as <see cref="Maybe{T}"/>.</summary>
    public partial Maybe<PhoneNumber> PhoneNumber { get; private set; }

    public ShippingAddress ShippingAddress { get; private set; } = null!;

    /// <summary>EF Core constructor.</summary>
    private Customer() : base(default!) { }

    public Customer(
        FirstName firstName,
        LastName lastName,
        EmailAddress email,
        Maybe<PhoneNumber> phoneNumber,
        ShippingAddress shippingAddress)
        : base(CustomerId.NewUniqueV7())
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        PhoneNumber = phoneNumber;
        ShippingAddress = shippingAddress;
    }
}
