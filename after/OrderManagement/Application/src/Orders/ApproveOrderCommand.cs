namespace OrderManagement.Application.Orders;

using FluentValidation;
using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>Approves a submitted order. Submitted → Approved.</summary>
public sealed record ApproveOrderCommand(OrderId OrderId) : ICommand<Result<Order>>, IAuthorize
{
    /// <inheritdoc />
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersApprove];
}

public sealed class ApproveOrderCommandValidator : AbstractValidator<ApproveOrderCommand>
{
    public ApproveOrderCommandValidator() => RuleFor(c => c.OrderId).NotNull();
}

public sealed class ApproveOrderCommandHandler(IOrderRepository repository, TimeProvider timeProvider)
    : ICommandHandler<ApproveOrderCommand, Result<Order>>
{
    public async ValueTask<Result<Order>> Handle(ApproveOrderCommand command, CancellationToken cancellationToken) =>
        await repository.FindByIdAsync(command.OrderId, cancellationToken)
            .ToResultAsync(() => Error.NotFound.For<Order>(
                id: command.OrderId, detail: $"Order {command.OrderId} not found."))
            .CheckAsync(order => order.Approve(timeProvider))
            .ConfigureAwait(false);
}
