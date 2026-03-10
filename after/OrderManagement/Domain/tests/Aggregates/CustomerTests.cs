namespace Domain.Tests.Aggregates;

using OrderManagement.Domain.Aggregates;
using OrderManagement.Domain.ValueObjects;
using Trellis.Primitives;

public class CustomerTests
{
    [Fact]
    public void TryCreate_ValidInput_ReturnsSuccess()
    {
        var result = CreateValidCustomer();

        result.Should().BeSuccess();
        var customer = result.Value;
        customer.FirstName.Value.Should().Be("John");
        customer.LastName.Value.Should().Be("Doe");
        customer.Email.Value.Should().Be("john@example.com");
        customer.PhoneNumber.HasValue.Should().BeFalse();
    }

    [Fact]
    public void TryCreate_WithPhoneNumber_SetsPhoneNumber()
    {
        var phone = PhoneNumber.TryCreate("+15551234567").Value;

        var result = Customer.TryCreate(
            CustomerFirstName.Create("Jane"),
            CustomerLastName.Create("Doe"),
            EmailAddress.Create("jane@example.com"),
            Maybe.From(phone),
            CreateValidAddress());

        result.Should().BeSuccess();
        result.Value.PhoneNumber.HasValue.Should().BeTrue();
    }

    private static Result<Customer> CreateValidCustomer() =>
        Customer.TryCreate(
            CustomerFirstName.Create("John"),
            CustomerLastName.Create("Doe"),
            EmailAddress.Create("john@example.com"),
            Maybe.None<PhoneNumber>(),
            CreateValidAddress());

    private static ShippingAddress CreateValidAddress() =>
        ShippingAddress.TryCreate(
            Street.Create("123 Main St"),
            City.Create("Anytown"),
            State.Create("WA"),
            PostalCode.Create("98052"),
            Country.Create("US")).Value;
}
