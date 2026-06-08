namespace OrderManagement.Application;

using Microsoft.Extensions.DependencyInjection;
using OrderManagement.Application.Orders;
using OrderManagement.Domain;
using Trellis.FluentValidation;
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

        // Resource authorization for ownership-checked cancel (spec §5.4). Registers
        // both the pipeline behavior and the v4 IAuthorizedResource<TMessage, TResource>
        // accessor used by CancelOrderCommandHandler to skip a duplicate Order load.
        services.AddResourceAuthorization<CancelOrderCommand, Order, Result<Order>>();

        return services;
    }
}
