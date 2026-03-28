namespace Application.Tests;

using Microsoft.Extensions.DependencyInjection;
using OrderManagement.Application;
using OrderManagement.Application.Orders;
using OrderManagement.Domain;
using Trellis.Authorization;
using Trellis.Mediator;
using Trellis.Testing.Fakes;

public static class DependencyInjection
{
    public static IServiceCollection AddMockDependencies(this IServiceCollection services)
    {
        var actorProvider = new TestActorProvider(
            "test-user",
            Permissions.CustomersCreate, Permissions.CustomersRead,
            Permissions.ProductsCreate, Permissions.ProductsRead, Permissions.ProductsManageStock,
            Permissions.OrdersCreate, Permissions.OrdersSubmit, Permissions.OrdersApprove,
            Permissions.OrdersShip, Permissions.OrdersDeliver, Permissions.OrdersCancel,
            Permissions.OrdersRead, Permissions.OrdersReadAll);

        services.AddSingleton<TestActorProvider>(actorProvider);
        services.AddSingleton<IActorProvider>(actorProvider);

        services.AddScoped<FakeRepository<Customer, CustomerId>>();
        services.AddScoped<FakeRepository<Product, ProductId>>();
        services.AddScoped<FakeRepository<Order, OrderId>>();

        services.AddScoped<ICustomerRepository, FakeCustomerRepositoryAdapter>();
        services.AddScoped<IProductRepository, FakeProductRepositoryAdapter>();
        services.AddScoped<IOrderRepository, FakeOrderRepositoryAdapter>();

        services.AddResourceAuthorization(
            typeof(CancelOrderCommand).Assembly,
            typeof(FakeCancelOrderResourceLoader).Assembly);

        return services;
    }
}
