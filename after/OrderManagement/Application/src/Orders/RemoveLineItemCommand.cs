namespace OrderManagement.Application.Orders;

using FluentValidation;
using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>Removes a line item from a draft order. Cannot remove the last line item.</summary>
public sealed record RemoveLineItemCommand(OrderId OrderId, LineItemId LineItemId)
    : ICommand<Result<Order>>, IAuthorize
{
    /// <inheritdoc />
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersCreate];
}

public sealed class RemoveLineItemCommandValidator : AbstractValidator<RemoveLineItemCommand>
{
    public RemoveLineItemCommandValidator()
    {
        RuleFor(c => c.OrderId).NotNull();
        RuleFor(c => c.LineItemId).NotNull();
    }
}

public sealed class RemoveLineItemCommandHandler(IOrderRepository repository)
    : ICommandHandler<RemoveLineItemCommand, Result<Order>>
{
    public async ValueTask<Result<Order>> Handle(RemoveLineItemCommand command, CancellationToken cancellationToken) =>
        await repository.FindByIdAsync(command.OrderId, cancellationToken)
            .ToResultAsync(() => Error.NotFound.For<Order>(
                id: command.OrderId, detail: $"Order {command.OrderId} not found."))
            .CheckAsync(order => order.RemoveLineItem(command.LineItemId))
            .ConfigureAwait(false);
}
