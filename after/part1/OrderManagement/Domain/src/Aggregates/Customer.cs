namespace OrderManagement.Domain;

using Trellis.Primitives;

/// <summary>
/// Customer aggregate.
/// </summary>
public partial class Customer : Aggregate<CustomerId>
{
    public FirstName FirstName { get; private set; } = null!;
    public LastName LastName { get; private set; } = null!;
    public EmailAddress Email { get; private set; } = null!;
    public partial Maybe<PhoneNumber> PhoneNumber { get; private set; }
    public ShippingAddress ShippingAddress { get; private set; } = null!;

    private Customer() : base(default!) { }

    private Customer(FirstName firstName, LastName lastName, EmailAddress email, Maybe<PhoneNumber> phoneNumber, ShippingAddress shippingAddress)
        : base(CustomerId.NewUniqueV7())
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        PhoneNumber = phoneNumber;
        ShippingAddress = shippingAddress;
    }

    public static Result<Customer> TryCreate(FirstName firstName, LastName lastName, EmailAddress email, Maybe<PhoneNumber> phoneNumber, ShippingAddress shippingAddress) =>
        new Customer(firstName, lastName, email, phoneNumber, shippingAddress);
}
