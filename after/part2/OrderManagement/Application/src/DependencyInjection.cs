namespace OrderManagement.Application;

using Microsoft.Extensions.DependencyInjection;
using Trellis.Mediator;

/// <summary>Extension methods for registering Application layer services.</summary>
public static class DependencyInjection
{
    /// <summary>Registers mediator and pipeline behaviors for the application layer.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediator(options => options.ServiceLifetime = ServiceLifetime.Scoped);
        services.AddTrellisBehaviors();
        return services;
    }
}
