namespace OrderManagement.AntiCorruptionLayer;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderManagement.Application.Abstractions;
using OrderManagement.Application.Commands;
using OrderManagement.AntiCorruptionLayer.Repositories;
using Trellis.EntityFrameworkCore;
using Trellis.Mediator;

public static class DependencyInjection
{
    public static IServiceCollection AddAntiCorruptionLayer(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=OrderManagement.db";

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(connectionString)
                   .AddInterceptors(new MaybeQueryInterceptor()));

        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();

        services.AddResourceAuthorization(
            typeof(CancelOrderCommand).Assembly,
            typeof(CancelOrderResourceLoader).Assembly);

        return services;
    }
}
