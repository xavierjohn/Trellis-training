namespace Application.Tests;

using Mediator;
using OrderManagement.Application.Customers;
using OrderManagement.Domain;
using Trellis.Authorization;
using Trellis.Primitives;
using Trellis.Testing.Fakes;

#pragma warning disable TRLS003 // Tests assert success before accessing .Value

public class CreateCustomerCommandTests
{
    private readonly ISender _sender;
    private readonly FakeRepository<Customer, CustomerId> _repo;
    private readonly TestActorProvider _actorProvider;

    public CreateCustomerCommandTests(
        ISender sender,
        FakeRepository<Customer, CustomerId> repo,
        TestActorProvider actorProvider)
    {
        _sender = sender;
        _repo = repo;
        _actorProvider = actorProvider;
    }

    private static CreateCustomerCommand MakeCommand() => new(
        CustomerFirstName.Create("Jane"),
        CustomerLastName.Create("Smith"),
        EmailAddress.Create("jane.smith@example.com"),
        Maybe<PhoneNumber>.None,
        ShippingAddress.TryCreate("1 Oak Ave", "Portland", "OR", "97201", "US").Value);

    [Fact]
    public async Task Create_customer_with_correct_permission_succeeds()
    {
        var result = await _sender.Send(MakeCommand(), TestContext.Current.CancellationToken);

        result.Should().BeSuccess();
        result.Value.FirstName.Value.Should().Be("Jane");
        result.Value.LastName.Value.Should().Be("Smith");
    }

    [Fact]
    public async Task Create_customer_without_permission_returns_forbidden()
    {
        await using var _ = _actorProvider.WithActor("no-perms-user");

        var result = await _sender.Send(MakeCommand(), TestContext.Current.CancellationToken);

        result.Should().BeFailureOfType<ForbiddenError>();
    }

    [Fact]
    public async Task Create_customer_is_persisted_in_repository()
    {
        var result = await _sender.Send(MakeCommand(), TestContext.Current.CancellationToken);

        result.Should().BeSuccess();
        var stored = await _repo.GetByIdAsync(result.Value.Id, TestContext.Current.CancellationToken);
        stored.Should().BeSuccess();
    }
}
