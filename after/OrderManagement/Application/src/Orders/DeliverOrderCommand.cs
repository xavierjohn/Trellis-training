namespace OrderManagement.Application.Orders;

using FluentValidation;
using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>Marks a shipped order as delivered. Shipped → Delivered.</summary>
public sealed record DeliverOrderCommand(OrderId OrderId) : ICommand<Result<Order>>, IAuthorize
{
    /// <inheritdoc />
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersDeliver];
}

public sealed class DeliverOrderCommandValidator : AbstractValidator<DeliverOrderCommand>
{
    public DeliverOrderCommandValidator() => RuleFor(c => c.OrderId).NotNull();
}

public sealed class DeliverOrderCommandHandler(IOrderRepository repository, TimeProvider timeProvider)
    : ICommandHandler<DeliverOrderCommand, Result<Order>>
{
    public async ValueTask<Result<Order>> Handle(DeliverOrderCommand command, CancellationToken cancellationToken) =>
        await repository.FindByIdAsync(command.OrderId, cancellationToken)
            .ToResultAsync(() => Error.NotFound.For<Order>(
                id: command.OrderId, detail: $"Order {command.OrderId} not found."))
            .CheckAsync(order => order.Deliver(timeProvider))
            .ConfigureAwait(false);
}
