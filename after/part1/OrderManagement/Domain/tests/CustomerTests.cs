namespace Domain.Tests;

using OrderManagement.Domain;
using Trellis.Primitives;

#pragma warning disable TRLS003 // Tests assert success before accessing .Value

public class CustomerTests
{
    private static CustomerFirstName TestFirstName => CustomerFirstName.Create("Jane");
    private static CustomerLastName TestLastName => CustomerLastName.Create("Smith");
    private static EmailAddress TestEmail => EmailAddress.Create("jane.smith@example.com");
    private static ShippingAddress TestAddress =>
        ShippingAddress.TryCreate("1 Oak Ave", "Portland", "OR", "97201", "US").Value;

    [Fact]
    public void TryCreate_with_all_required_fields_succeeds()
    {
        var result = Customer.TryCreate(TestFirstName, TestLastName, TestEmail, Maybe<PhoneNumber>.None, TestAddress);

        result.Should().BeSuccess();
        var customer = result.Value;
        customer.FirstName.Should().Be(TestFirstName);
        customer.LastName.Should().Be(TestLastName);
        customer.Email.Should().Be(TestEmail);
        customer.PhoneNumber.Should().BeNone();
    }

    [Fact]
    public void TryCreate_with_phone_number_preserves_phone()
    {
        var phone = PhoneNumber.Create("+15035550101");

        var result = Customer.TryCreate(TestFirstName, TestLastName, TestEmail, Maybe.From(phone), TestAddress);

        result.Should().BeSuccess();
        result.Value.PhoneNumber.Should().HaveValueEqualTo(phone);
    }

    [Fact]
    public void TryCreate_without_phone_number_has_none()
    {
        var result = Customer.TryCreate(TestFirstName, TestLastName, TestEmail, Maybe<PhoneNumber>.None, TestAddress);

        result.Should().BeSuccess();
        result.Value.PhoneNumber.Should().BeNone();
    }

    [Fact]
    public void CustomerFirstName_blank_fails_validation()
    {
        var result = CustomerFirstName.TryCreate("");

        result.Should().BeFailure();
    }

    [Fact]
    public void EmailAddress_invalid_format_fails_validation()
    {
        var result = EmailAddress.TryCreate("not-a-valid-email");

        result.Should().BeFailure();
    }
}
