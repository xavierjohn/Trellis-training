namespace OrderManagement.Application.Commands;

using Mediator;
using OrderManagement.Application.Abstractions;
using Trellis.Authorization;
using Trellis.Primitives;

public sealed class CreateDraftOrderCommandHandler(
    ICustomerRepository customerRepository,
    IProductRepository productRepository,
    IOrderRepository orderRepository,
    IActorProvider actorProvider)
    : ICommandHandler<CreateDraftOrderCommand, Result<Order>>
{
    public async ValueTask<Result<Order>> Handle(CreateDraftOrderCommand command, CancellationToken cancellationToken)
    {
        var actorId = actorProvider.GetCurrentActor().Id;
        var productIds = command.LineItems.Select(li => li.ProductId).ToList();

        return await Result.ParallelAsync(
                () => customerRepository.GetByIdAsync(command.CustomerId, cancellationToken),
                () => productRepository.GetByIdsAsync(productIds, cancellationToken))
            .WhenAllAsync()
            .BindAsync((Customer customer, List<Product> products) =>
                BuildOrder(customer, products, command.LineItems, actorId))
            .TapAsync(order => orderRepository.SaveAsync(order, cancellationToken));
    }

    private static Result<Order> BuildOrder(
        Customer customer,
        List<Product> products,
        List<LineItemRequest> lineItems,
        string actorId)
    {
        var tuples = new List<(ProductId, string, int, Money)>(lineItems.Count);
        foreach (var req in lineItems)
        {
            var product = products.FirstOrDefault(p => p.Id == req.ProductId);
            if (product is null)
                return Error.NotFound($"Product '{req.ProductId}' not found");
            tuples.Add((req.ProductId, product.ProductName.Value, req.Quantity, product.UnitPrice));
        }

        return Order.TryCreate(customer.Id, actorId, tuples);
    }
}
