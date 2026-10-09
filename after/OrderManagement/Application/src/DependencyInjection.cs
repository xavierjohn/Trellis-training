namespace OrderManagement.Application;

using Microsoft.Extensions.DependencyInjection;
using OrderManagement.Application.Orders;
using OrderManagement.Domain;
using Trellis.Mediator;
using Trellis.Mediator.FluentValidation;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddMediator(options => options.ServiceLifetime = ServiceLifetime.Scoped);
        services.AddTrellisBehaviors();
        services.AddDomainEventDispatch(typeof(CreateDraftOrderCommandHandler).Assembly);
        services.AddTrellisFluentValidation(typeof(CreateDraftOrderCommandValidator).Assembly);

        services.AddResourceAuthorization(typeof(CancelOrderCommand).Assembly);

        return services;
    }
}
