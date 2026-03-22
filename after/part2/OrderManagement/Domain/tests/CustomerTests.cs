namespace Domain.Tests;

public class CustomerTests
{
    [Fact]
    public void TryCreate_WithValidData_ReturnsCustomer()
    {
        var firstName = FirstName.Create("John");
        var lastName = LastName.Create("Doe");
        var email = EmailAddress.Create("john.doe@example.com");
        ShippingAddress.TryCreate("123 Main St", "Anytown", "CA", "12345", "USA").TryGetValue(out var address);

        var result = Customer.TryCreate(firstName, lastName, email, Maybe<PhoneNumber>.None, address!);

        result.Should().BeSuccess()
            .Which.Email.Should().Be(email);
    }

    [Fact]
    public void TryCreate_WithPhone_IncludesPhone()
    {
        var firstName = FirstName.Create("Jane");
        var lastName = LastName.Create("Smith");
        var email = EmailAddress.Create("jane@example.com");
        var phone = PhoneNumber.Create("+14155551234");
        ShippingAddress.TryCreate("456 Elm St", "Springfield", "IL", "62701", "USA").TryGetValue(out var address);

        var result = Customer.TryCreate(firstName, lastName, email, phone, address!);

        result.Should().BeSuccess()
            .Which.PhoneNumber.Should().HaveValue();
    }

    [Fact]
    public void ShippingAddress_WithMissingStreet_ReturnsValidationError()
    {
        var result = ShippingAddress.TryCreate("", "City", "State", "12345", "USA");

        result.Should().BeFailure()
            .Which.Should().BeOfType<ValidationError>();
    }
}
