#pragma warning disable TRLS001, TRLS003

namespace Domain.Tests;

using OrderManagement.Domain;
using Trellis.Primitives;
using Trellis.Testing;

public class CustomerTests
{
    private static FirstName ValidFirstName => FirstName.Create("John");
    private static LastName ValidLastName => LastName.Create("Doe");
    private static EmailAddress ValidEmail => EmailAddress.Create("john@example.com");
    private static PhoneNumber ValidPhone => PhoneNumber.Create("+12025551234");
    private static ShippingAddress ValidAddress => ShippingAddress.TryCreate(
        Street.Create("123 Main St"), City.Create("Springfield"),
        State.Create("IL"), PostalCode.Create("62701"), Country.Create("USA")).Value;

    [Fact]
    public void TryCreate_ValidWithPhone_ReturnsSuccess()
    {
        var result = Customer.TryCreate(ValidFirstName, ValidLastName, ValidEmail, ValidPhone, ValidAddress);

        result.Should().BeSuccess();
        result.Value.FirstName.Should().Be(ValidFirstName);
        result.Value.LastName.Should().Be(ValidLastName);
        result.Value.Email.Should().Be(ValidEmail);
        result.Value.PhoneNumber.Should().HaveValue();
        result.Value.ShippingAddress.Should().Be(ValidAddress);
    }

    [Fact]
    public void TryCreate_ValidWithoutPhone_ReturnsSuccess()
    {
        var result = Customer.TryCreate(ValidFirstName, ValidLastName, ValidEmail, Maybe<PhoneNumber>.None, ValidAddress);

        result.Should().BeSuccess();
        result.Value.PhoneNumber.Should().BeNone();
    }
}
