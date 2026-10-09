namespace OrderManagement.Application.Orders;

using FluentValidation;
using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>Ships an approved order. Approved → Shipped.</summary>
public sealed record ShipOrderCommand(OrderId OrderId) : ICommand<Result<Order>>, IAuthorize
{
    /// <inheritdoc />
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersShip];
}

public sealed class ShipOrderCommandValidator : AbstractValidator<ShipOrderCommand>
{
    public ShipOrderCommandValidator() => RuleFor(c => c.OrderId).NotNull();
}

public sealed class ShipOrderCommandHandler(IOrderRepository repository, TimeProvider timeProvider)
    : ICommandHandler<ShipOrderCommand, Result<Order>>
{
    public async ValueTask<Result<Order>> Handle(ShipOrderCommand command, CancellationToken cancellationToken) =>
        await repository.FindByIdAsync(command.OrderId, cancellationToken)
            .ToResultAsync(() => Error.NotFound.For<Order>(
                id: command.OrderId, detail: $"Order {command.OrderId} not found."))
            .CheckAsync(order => order.Ship(timeProvider))
            .ConfigureAwait(false);
}
