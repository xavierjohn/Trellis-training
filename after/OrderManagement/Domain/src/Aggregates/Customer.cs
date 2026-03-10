namespace OrderManagement.Domain.Aggregates;

using OrderManagement.Domain.ValueObjects;
using Trellis.Primitives;

public partial class Customer : Aggregate<CustomerId>
{
    public CustomerFirstName FirstName { get; private set; } = null!;
    public CustomerLastName LastName { get; private set; } = null!;
    public EmailAddress Email { get; private set; } = null!;
    public partial Maybe<PhoneNumber> PhoneNumber { get; set; }
    public ShippingAddress ShippingAddress { get; private set; } = null!;

    private Customer() : base(default!) { }

    public static Result<Customer> TryCreate(
        CustomerFirstName firstName,
        CustomerLastName lastName,
        EmailAddress email,
        Maybe<PhoneNumber> phoneNumber,
        ShippingAddress shippingAddress)
    {
        var customer = new Customer
        {
            Id = CustomerId.NewUniqueV7(),
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            PhoneNumber = phoneNumber,
            ShippingAddress = shippingAddress
        };

        return customer;
    }
}
