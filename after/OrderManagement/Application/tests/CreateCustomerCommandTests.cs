namespace Application.Tests;

using Mediator;
using Microsoft.Extensions.DependencyInjection;
using OrderManagement.Application;
using OrderManagement.Application.Customers;
using OrderManagement.Domain;
using Trellis.Authorization;
using Trellis.Primitives;
using Trellis.Testing;

public class CreateCustomerCommandTests
{
    [Fact]
    public async Task CreateCustomer_Succeeds_WhenEmailIsUnique()
    {
        var (sender, _) = BuildHost();
        var command = NewCommand("ada@example.com");

        var result = await sender.Send(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Unwrap().Email.Value.Should().Be("ada@example.com");
    }

    [Fact]
    public async Task CreateCustomer_ReturnsConflict_WhenEmailAlreadyExists()
    {
        var (sender, _) = BuildHost();
        var first = await sender.Send(NewCommand("dup@example.com"), CancellationToken.None);
        first.IsSuccess.Should().BeTrue();

        var second = await sender.Send(NewCommand("dup@example.com"), CancellationToken.None);

        second.IsFailure.Should().BeTrue();
        second.Error.Should().BeOfType<Error.Conflict>();
    }

    [Fact]
    public async Task CreateCustomer_ReturnsForbidden_WhenActorLacksPermission()
    {
        // Override actor with NO customers:create permission.
        var (sender, _) = BuildHost(services =>
        {
            var noPermActor = new TestActorProvider("actor-x"); // empty permission set
            services.AddSingleton(noPermActor);
            services.AddSingleton<IActorProvider>(noPermActor);
        });

        var result = await sender.Send(NewCommand("nobody@example.com"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<Error.Forbidden>();
    }

    private static CreateCustomerCommand NewCommand(string email) => new(
        FirstName.Create("Ada"),
        LastName.Create("Lovelace"),
        EmailAddress.Create(email),
        Maybe<PhoneNumber>.None,
        new ShippingAddress(
            Street.Create("1 Compute Way"),
            City.Create("Palo Alto"),
            StateRegion.Create("CA"),
            PostalCode.Create("94301"),
            Country.Create("USA")));

    private static (ISender sender, IServiceProvider services) BuildHost(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddApplication().AddMockDependencies();
        configure?.Invoke(services);
        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();
        return (scope.ServiceProvider.GetRequiredService<ISender>(), scope.ServiceProvider);
    }
}