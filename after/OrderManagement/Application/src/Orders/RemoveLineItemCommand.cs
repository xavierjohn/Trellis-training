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

public sealed class RemoveLineItemCommandHandler : ICommandHandler<RemoveLineItemCommand, Result<Order>>
{
    private readonly IOrderRepository _repository;

    public RemoveLineItemCommandHandler(IOrderRepository repository) => _repository = repository;

    public async ValueTask<Result<Order>> Handle(RemoveLineItemCommand command, CancellationToken cancellationToken)
    {
        var orderMaybe = await _repository.FindByIdAsync(command.OrderId, cancellationToken);
        if (!orderMaybe.TryGetValue(out var order))
            return Result.Fail<Order>(new Error.NotFound(ResourceRef.For<Order>(command.OrderId))
            { Detail = $"Order {command.OrderId} not found." });

        return order.RemoveLineItem(command.LineItemId).Map(_ => order);
    }
}
