namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Application.Repositories;
using OrderManagement.Domain.Aggregates;
using OrderManagement.Domain.ValueObjects;
using Trellis.Authorization;

public sealed class CreateDraftOrderHandler(
    ICustomerRepository customerRepository,
    IProductRepository productRepository,
    IOrderRepository orderRepository,
    IActorProvider actorProvider)
    : ICommandHandler<CreateDraftOrderCommand, Result<Order>>
{
    public async ValueTask<Result<Order>> Handle(CreateDraftOrderCommand command, CancellationToken cancellationToken)
    {
        var productIds = command.LineItems.Select(li => li.ProductId).ToList();

        return await Result.ParallelAsync(
                () => customerRepository.GetByIdAsync(command.CustomerId, cancellationToken),
                () => productRepository.GetByIdsAsync(productIds, cancellationToken))
            .WhenAllAsync()
            .BindAsync((Customer _, List<Product> products) =>
            {
                var lineItems = new List<LineItem>();
                foreach (var input in command.LineItems)
                {
                    var product = products.FirstOrDefault(p => p.Id == input.ProductId);
                    if (product is null)
                        return Result.Failure<List<LineItem>>(Error.NotFound($"Product '{input.ProductId}' not found."));

                    var lineItemResult = LineItem.TryCreate(
                        product.Id,
                        product.ProductName,
                        input.Quantity,
                        product.UnitPrice);

                    if (lineItemResult.TryGetError(out var error))
                        return error;

                    lineItemResult.TryGetValue(out var lineItem);
                    lineItems.Add(lineItem);
                }

                return Result.Success(lineItems);
            })
            .BindAsync(lineItems =>
            {
                var actor = actorProvider.GetCurrentActor();
                return ActorId.TryCreate(actor.Id)
                    .Bind(actorId => Order.TryCreate(command.CustomerId, actorId, lineItems));
            })
            .BindAsync((Func<Order, Task<Result<Order>>>)(async order =>
                await orderRepository.SaveAsync(order, cancellationToken)));
    }
}
