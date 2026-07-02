namespace OrderManagement.AntiCorruptionLayer;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrderManagement.AntiCorruptionLayer.Eventing;
using OrderManagement.Application.Customers;
using OrderManagement.Application.IntegrationEvents;
using OrderManagement.Application.Orders;
using OrderManagement.Application.Products;
using OrderManagement.Domain;
using Trellis.Authorization;
using Trellis.EntityFrameworkCore;
using Trellis.Mediator;

public static class DependencyInjection
{
    public static IServiceCollection AddAntiCorruptionLayer(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(connectionString)
                   .AddTrellisInterceptors()
                   .AddTrellisOutboxInterceptor());

        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();

        // Shared loader used by every IIdentifyResource<Order, OrderId> command
        // (currently: CancelOrderCommand).
        services.AddScoped<SharedResourceLoaderById<Order, OrderId>, OrderResourceLoader>();

        services.AddTrellisUnitOfWork<AppDbContext>();

        // Transactional outbox: integration events staged by the domain-event translators are
        // written in the SAME transaction as the aggregate change, then relayed after commit —
        // no lost events, no dual-write to the broker inside the request.
        services.AddTrellisOutbox<AppDbContext>();

        // Idempotent inbox: inbound integration events are de-duplicated by event id per consumer,
        // so a redelivered PaymentConfirmed is processed at most once.
        services.AddTrellisInbox<AppDbContext>(o => o.ConsumerId = "order-management");

        // Route integration events through the in-memory broker instead of the default in-process
        // fan-out, so the payment simulator and the inbox consumer observe the same messages a real
        // broker would carry.
        services.RemoveAll<IIntegrationEventPublisher>();
        services.AddSingleton<InMemoryEventBus>();
        services.AddSingleton<IIntegrationEventPublisher, BrokerIntegrationEventPublisher>();

        // Inbound payment confirmation: the broker consumer feeds the idempotent inbox, which
        // dispatches to the hardened PaymentConfirmedHandler.
        services.AddIntegrationEventHandler<PaymentConfirmedIntegrationEvent, PaymentConfirmedHandler>();
        services.AddHostedService<PaymentConfirmedConsumer>();

        return services;
    }

    /// <summary>
    /// Registers the development-only payment simulator, which auto-confirms payment shortly
    /// after an order is submitted by publishing a <c>PaymentConfirmed</c> event back onto the
    /// broker. Call only in non-production environments.
    /// </summary>
    public static IServiceCollection AddDevelopmentPaymentSimulator(this IServiceCollection services)
    {
        services.AddHostedService<PaymentSimulator>();
        return services;
    }
}
