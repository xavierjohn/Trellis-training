namespace Application.Tests;

using Mediator;
using Microsoft.Extensions.DependencyInjection;
using OrderManagement.Application;
using OrderManagement.Application.Orders;
using OrderManagement.Domain;
using Trellis.Authorization;
using Trellis.Mediator;
using Trellis.Testing.Fakes;

public sealed class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddApplication();

        services.AddSingleton<TestActorProvider>(_ =>
            new TestActorProvider("admin",
                Permissions.CustomersCreate, Permissions.CustomersRead,
                Permissions.ProductsCreate, Permissions.ProductsRead, Permissions.ProductsManageStock,
                Permissions.OrdersCreate, Permissions.OrdersRead, Permissions.OrdersReadAll,
                Permissions.OrdersSubmit, Permissions.OrdersApprove, Permissions.OrdersShip,
                Permissions.OrdersDeliver, Permissions.OrdersCancel));
        services.AddSingleton<IActorProvider>(sp => sp.GetRequiredService<TestActorProvider>());

        services.AddScoped<FakeCustomerRepository>();
        services.AddScoped<FakeProductRepository>();
        services.AddScoped<FakeOrderRepository>();

        services.AddScoped<ICustomerRepository>(sp => sp.GetRequiredService<FakeCustomerRepository>());
        services.AddScoped<IProductRepository>(sp => sp.GetRequiredService<FakeProductRepository>());
        services.AddScoped<IOrderRepository>(sp => sp.GetRequiredService<FakeOrderRepository>());

        // Register resource authorization behavior for CancelOrderCommand
        services.AddResourceAuthorization<CancelOrderCommand, Order, Result<Order>>();
        services.ReplaceResourceLoader<CancelOrderCommand, Order>(sp =>
            new FakeCancelOrderResourceLoader(sp.GetRequiredService<FakeOrderRepository>()));
    }
}
