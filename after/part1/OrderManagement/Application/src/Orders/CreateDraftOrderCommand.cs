namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Application.Customers;
using OrderManagement.Application.Products;
using OrderManagement.Domain;
using Trellis.Authorization;
using Trellis.Mediator;

/// <summary>
/// Input for a line item in a create draft order command.
/// </summary>
public sealed record CreateOrderLineItem(ProductId ProductId, LineItemQuantity Quantity);

/// <summary>
/// Creates a draft order.
/// </summary>
public sealed record CreateDraftOrderCommand(
    CustomerId CustomerId,
    IReadOnlyList<CreateOrderLineItem> LineItems) : ICommand<Result<Order>>, IAuthorize, IValidate
{
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersCreate];

    public IResult Validate()
    {
        if (LineItems.Count == 0)
            return Result.Failure(Error.Validation("At least one line item is required.", "lineItems"));

        var productIds = LineItems.Select(li => li.ProductId).ToList();
        if (productIds.Distinct().Count() != productIds.Count)
            return Result.Failure(Error.Validation("Duplicate product IDs are not allowed. Combine quantities instead.", "lineItems"));

        return Result.Success();
    }
}

/// <summary>
/// Handler for CreateDraftOrderCommand.
/// </summary>
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
        var productIds = command.LineItems.Select(li => li.ProductId).ToList();

        var customerMaybe = await _customerRepository.FindByIdAsync(command.CustomerId, cancellationToken);
        var customerResult = customerMaybe.ToResult(Error.NotFound($"Customer {command.CustomerId} not found."));
        if (customerResult.IsFailure) return customerResult.Error;

        var products = await _productRepository.GetByIdsAsync(productIds, cancellationToken);

        // Verify all products were found
        foreach (var lineItem in command.LineItems)
        {
            if (!products.Any(p => p.Id == lineItem.ProductId))
                return Error.NotFound($"Product {lineItem.ProductId} not found.");
        }

        var lineItems = command.LineItems.Select(li =>
        {
            var product = products.First(p => p.Id == li.ProductId);
            return LineItem.Create(li.ProductId, product.ProductName, li.Quantity, product.UnitPrice);
        }).ToList();

        var actor = _actorProvider.GetCurrentActor();
        return await Order.TryCreate(command.CustomerId, actor.Id, lineItems)
            .BindAsync(order => _orderRepository.SaveAsync(order, cancellationToken).MapAsync(_ => order));
    }
}
