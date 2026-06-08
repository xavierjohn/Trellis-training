namespace Application.Tests;

using Microsoft.Extensions.DependencyInjection;
using OrderManagement.Application;
using OrderManagement.Application.Customers;
using OrderManagement.Application.Orders;
using OrderManagement.Application.Products;
using OrderManagement.Domain;
using Trellis.Authorization;
using Trellis.Testing;

public static class DependencyInjection
{
    public const string DefaultActorId = "actor-tester";

    public static IServiceCollection AddMockDependencies(this IServiceCollection services)
    {
        services.AddLogging();

        // Default actor has every OM permission so the bulk of tests can exercise the
        // happy path without re-configuring the actor. Permission/ownership tests
        // override the registration with a narrower actor before sending.
        var actorProvider = new TestActorProvider(
            DefaultActorId,
            Permissions.CustomersCreate,
            Permissions.ProductsCreate,
            Permissions.ProductsManageStock,
            Permissions.OrdersCreate,
            Permissions.OrdersSubmit,
            Permissions.OrdersApprove,
            Permissions.OrdersShip,
            Permissions.OrdersDeliver,
            Permissions.OrdersCancel,
            Permissions.OrdersRead,
            Permissions.OrdersReadAll);
        services.AddSingleton(actorProvider);
        services.AddSingleton<IActorProvider>(actorProvider);
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<FakeRepository<Customer, CustomerId>>();
        services.AddScoped<FakeRepository<Product, ProductId>>();
        services.AddScoped<FakeRepository<Order, OrderId>>();

        services.AddScoped<ICustomerRepository, FakeCustomerRepository>();
        services.AddScoped<IProductRepository, FakeProductRepository>();
        services.AddScoped<IOrderRepository, FakeOrderRepository>();

        services.AddScoped<SharedResourceLoaderById<Order, OrderId>, FakeOrderResourceLoader>();
        return services;
    }
}
