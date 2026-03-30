namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;
using Trellis.Mediator;

public sealed record CreateDraftOrderLineItemInput(ProductId ProductId, LineItemQuantity Quantity);

public sealed record CreateDraftOrderCommand(
    CustomerId CustomerId,
    List<CreateDraftOrderLineItemInput> LineItems) : ICommand<Result<Order>>, IAuthorize, IValidate
{
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersCreate];

    public IResult Validate()
    {
        if (LineItems.Count == 0)
            return Result.Failure(Error.Validation("At least one line item is required.", "lineItems"));

        var duplicateProductIds = LineItems.GroupBy(li => li.ProductId).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicateProductIds.Count > 0)
            return Result.Failure(Error.Validation("Duplicate products are not allowed in the same order.", "lineItems"));

        return Result.Success();
    }
}

public sealed class CreateDraftOrderCommandHandler : ICommandHandler<CreateDraftOrderCommand, Result<Order>>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IActorProvider _actorProvider;

    public CreateDraftOrderCommandHandler(
        ICustomerRepository customerRepository,
        IProductRepository productRepository,
        IOrderRepository orderRepository,
        IActorProvider actorProvider)
    {
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _orderRepository = orderRepository;
        _actorProvider = actorProvider;
    }

    public async ValueTask<Result<Order>> Handle(CreateDraftOrderCommand command, CancellationToken cancellationToken)
    {
        var actor = await _actorProvider.GetCurrentActorAsync(cancellationToken);
        var productIds = command.LineItems.Select(li => li.ProductId).ToList();

        var customerResult = (await _customerRepository.FindByIdAsync(command.CustomerId, cancellationToken))
            .ToResult(Error.NotFound($"Customer {command.CustomerId.Value} not found."));
        if (customerResult.IsFailure)
            return customerResult.Error;

        var products = await _productRepository.GetByIdsAsync(productIds, cancellationToken);
        if (products.Count != productIds.Count)
        {
            var missingIds = productIds.Where(id => !products.Any(p => p.Id == id)).Select(id => id.Value.ToString());
            return Error.NotFound($"Products not found: {string.Join(", ", missingIds)}");
        }

        var lineItems = new List<LineItem>();
        foreach (var input in command.LineItems)
        {
            var product = products.First(p => p.Id == input.ProductId);
            lineItems.Add(new LineItem(input.ProductId, product.ProductName, input.Quantity, product.UnitPrice));
        }

        return await Order.TryCreate(command.CustomerId, actor.Id, lineItems)
            .CheckAsync(order => _orderRepository.SaveAsync(order, cancellationToken));
    }
}
