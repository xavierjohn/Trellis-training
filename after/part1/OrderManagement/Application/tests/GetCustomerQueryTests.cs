namespace Application.Tests;

using Mediator;
using OrderManagement.Application.Customers;
using OrderManagement.Domain;
using Trellis.Primitives;
using Trellis.Testing.Fakes;

#pragma warning disable TRLS003

public class GetCustomerQueryTests
{
    private readonly ISender _sender;
    private readonly FakeCustomerRepository _customerRepo;

    public GetCustomerQueryTests(ISender sender, FakeCustomerRepository customerRepo)
    {
        _sender = sender;
        _customerRepo = customerRepo;
    }

    [Fact]
    public async Task GetCustomer_not_found_returns_not_found()
    {
        var result = await _sender.Send(new GetCustomerQuery(CustomerId.NewUniqueV7()));

        result.Should().BeFailure()
            .Which.Should().BeOfType<NotFoundError>();
    }

    [Fact]
    public async Task GetCustomer_exists_returns_success()
    {
        var customer = Customer.TryCreate(
            FirstName.Create("Test"),
            LastName.Create("User"),
            EmailAddress.Create($"test-{Guid.NewGuid():N}@example.com"),
            Maybe<PhoneNumber>.None,
            ShippingAddress.TryCreate(Street.Create("1 St"), City.Create("City"), State.Create("ST"), PostalCode.Create("00000"), Country.Create("US")).Value).Value;
        var saveResult = await _customerRepo.SaveAsync(customer, CancellationToken.None);
        saveResult.Should().BeSuccess();

        var result = await _sender.Send(new GetCustomerQuery(customer.Id));

        result.Should().BeSuccess()
            .Which.Id.Should().Be(customer.Id);
    }
}
