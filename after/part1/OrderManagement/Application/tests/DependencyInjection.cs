namespace Application.Tests;

using Application.Tests.Fakes;
using OrderManagement.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Trellis.Authorization;
using Trellis.Testing.Fakes;

public static class DependencyInjection
{
    public static IServiceCollection AddMockAntiCorruptionLayer(this IServiceCollection services)
    {
        var actorProvider = new TestActorProvider(
            "test-user",
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

        services.AddSingleton<IActorProvider>(actorProvider);
        services.AddSingleton<IAsyncActorProvider>(actorProvider);
        services.AddSingleton<TestActorProvider>(actorProvider);

        services.AddSingleton<ICustomerRepository, FakeCustomerRepository>();
        services.AddSingleton<IProductRepository, FakeProductRepository>();
        services.AddSingleton<IOrderRepository, FakeOrderRepository>();

        return services;
    }
}
