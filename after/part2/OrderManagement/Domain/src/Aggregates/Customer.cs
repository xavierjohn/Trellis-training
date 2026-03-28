namespace OrderManagement.Domain;

using Trellis.Primitives;

/// <summary>
/// A customer who places orders. Contains contact info and a default shipping address.
/// </summary>
public partial class Customer : Aggregate<CustomerId>
{
    /// <summary>Customer's first name.</summary>
    public CustomerFirstName FirstName { get; private set; } = null!;

    /// <summary>Customer's last name.</summary>
    public CustomerLastName LastName { get; private set; } = null!;

    /// <summary>Customer's email address. Must be unique.</summary>
    public EmailAddress Email { get; private set; } = null!;

    /// <summary>Optional phone number.</summary>
    public partial Maybe<PhoneNumber> PhoneNumber { get; private set; }

    /// <summary>Default shipping address.</summary>
    public ShippingAddress ShippingAddress { get; private set; } = null!;

    /// <summary>EF Core constructor.</summary>
    private Customer() : base(default!) { }

    private Customer(
        CustomerFirstName firstName,
        CustomerLastName lastName,
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

    /// <summary>Creates a new customer.</summary>
    public static Result<Customer> TryCreate(
        CustomerFirstName firstName,
        CustomerLastName lastName,
        EmailAddress email,
        Maybe<PhoneNumber> phoneNumber,
        ShippingAddress shippingAddress) =>
        new Customer(firstName, lastName, email, phoneNumber, shippingAddress);
}
