namespace Application.Tests;

using Mediator;
using OrderManagement.Application.Customers;
using OrderManagement.Domain;
using Trellis.Primitives;
using Trellis.Testing.Fakes;

#pragma warning disable TRLS003

public class CreateCustomerCommandTests
{
    private readonly ISender _sender;
    private readonly TestActorProvider _actorProvider;

    public CreateCustomerCommandTests(ISender sender, TestActorProvider actorProvider)
    {
        _sender = sender;
        _actorProvider = actorProvider;
    }

    [Fact]
    public async Task CreateCustomer_valid_returns_success()
    {
        var command = new CreateCustomerCommand(
            FirstName.Create("John"),
            LastName.Create("Doe"),
            EmailAddress.Create($"john-{Guid.NewGuid():N}@example.com"),
            Maybe.From(PhoneNumber.Create("+12025551234")),
            ShippingAddress.TryCreate(Street.Create("123 Main"), City.Create("Seattle"), State.Create("WA"), PostalCode.Create("98101"), Country.Create("US")).Value);

        var result = await _sender.Send(command);

        result.Should().BeSuccess()
            .Which.FirstName.Value.Should().Be("John");
    }

    [Fact]
    public async Task CreateCustomer_missing_permission_returns_forbidden()
    {
        await using var scope = _actorProvider.WithActor("no-perms");

        var command = new CreateCustomerCommand(
            FirstName.Create("Jane"),
            LastName.Create("Doe"),
            EmailAddress.Create($"jane-{Guid.NewGuid():N}@example.com"),
            Maybe<PhoneNumber>.None,
            ShippingAddress.TryCreate(Street.Create("456 Oak"), City.Create("Portland"), State.Create("OR"), PostalCode.Create("97201"), Country.Create("US")).Value);

        var result = await _sender.Send(command);

        result.Should().BeFailure()
            .Which.Should().BeOfType<ForbiddenError>();
    }
}
