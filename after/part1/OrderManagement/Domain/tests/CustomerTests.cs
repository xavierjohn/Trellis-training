namespace Domain.Tests;

using OrderManagement.Domain;
using Trellis.Primitives;

#pragma warning disable TRLS003

public class CustomerTests
{
    [Fact]
    public void TryCreate_valid_customer_with_phone_succeeds()
    {
        var result = Customer.TryCreate(
            FirstName.Create("John"),
            LastName.Create("Doe"),
            EmailAddress.Create("john@example.com"),
            Maybe.From(PhoneNumber.Create("+12025551234")),
            ShippingAddress.TryCreate(Street.Create("123 Main"), City.Create("Seattle"), State.Create("WA"), PostalCode.Create("98101"), Country.Create("US")).Value);

        result.Should().BeSuccess();
        var customer = result.Value;
        customer.FirstName.Value.Should().Be("John");
        customer.LastName.Value.Should().Be("Doe");
        customer.Email.Value.Should().Be("john@example.com");
        customer.PhoneNumber.Should().HaveValue();
    }

    [Fact]
    public void TryCreate_valid_customer_without_phone_succeeds()
    {
        var result = Customer.TryCreate(
            FirstName.Create("Jane"),
            LastName.Create("Doe"),
            EmailAddress.Create("jane@example.com"),
            Maybe<PhoneNumber>.None,
            ShippingAddress.TryCreate(Street.Create("456 Oak"), City.Create("Portland"), State.Create("OR"), PostalCode.Create("97201"), Country.Create("US")).Value);

        result.Should().BeSuccess();
        result.Value.PhoneNumber.Should().BeNone();
    }
}
