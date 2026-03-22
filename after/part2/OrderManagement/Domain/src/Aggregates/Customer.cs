namespace OrderManagement.Domain;

using Trellis.Primitives;

public partial class Customer : Aggregate<CustomerId>
{
    public FirstName FirstName { get; private set; } = default!;
    public LastName LastName { get; private set; } = default!;
    public EmailAddress Email { get; private set; } = default!;
    public partial Maybe<PhoneNumber> PhoneNumber { get; set; }
    public ShippingAddress ShippingAddress { get; private set; } = default!;

    public static Result<Customer> TryCreate(
        FirstName firstName,
        LastName lastName,
        EmailAddress email,
        Maybe<PhoneNumber> phoneNumber,
        ShippingAddress shippingAddress)
    {
        var customer = new Customer(
            CustomerId.NewUniqueV4(),
            firstName,
            lastName,
            email,
            phoneNumber,
            shippingAddress);
        return customer;
    }

    private Customer(
        CustomerId id,
        FirstName firstName,
        LastName lastName,
        EmailAddress email,
        Maybe<PhoneNumber> phoneNumber,
        ShippingAddress shippingAddress) : base(id)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        PhoneNumber = phoneNumber;
        ShippingAddress = shippingAddress;
    }

    // EF Core constructor
    private Customer() : base(default!)
    {
    }
}
