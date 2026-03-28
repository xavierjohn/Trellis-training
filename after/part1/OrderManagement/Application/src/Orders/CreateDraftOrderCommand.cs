namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>
/// Creates a new draft order for a customer.
/// Cross-field validation (duplicate products, minimum line items) is enforced by <see cref="TryCreate"/>.
/// </summary>
public sealed class CreateDraftOrderCommand : ICommand<Result<Order>>, IAuthorize
{
    /// <summary>Input for a single line item within the command.</summary>
    public readonly record struct LineItemInput(ProductId ProductId, LineItemQuantity Quantity);

    /// <summary>The customer placing the order.</summary>
    public CustomerId CustomerId { get; }

    /// <summary>The requested line items. Must be non-empty with no duplicate product IDs.</summary>
    public IReadOnlyList<LineItemInput> LineItems { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersCreate];

    private CreateDraftOrderCommand(CustomerId customerId, IReadOnlyList<LineItemInput> lineItems)
    {
        CustomerId = customerId;
        LineItems = lineItems;
    }

    /// <summary>
    /// Creates the command, validating that at least one line item is present and product IDs are unique.
    /// </summary>
    public static Result<CreateDraftOrderCommand> TryCreate(
        CustomerId customerId,
        IReadOnlyList<LineItemInput> lineItems) =>
        Result.Ensure(lineItems.Count > 0,
                Error.Validation("Order must have at least one line item.", "lineItems"))
            .Ensure(() => lineItems.Select(li => li.ProductId).Distinct().Count() == lineItems.Count,
                Error.Validation("Duplicate product IDs are not allowed.", "lineItems"))
            .Map(_ => new CreateDraftOrderCommand(customerId, lineItems));
}

/// <summary>Handler for <see cref="CreateDraftOrderCommand"/>.</summary>
public sealed class CreateDraftOrderCommandHandler : ICommandHandler<CreateDraftOrderCommand, Result<Order>>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IActorProvider _actorProvider;

    /// <summary>Initializes a new instance of <see cref="CreateDraftOrderCommandHandler"/>.</summary>
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

    /// <inheritdoc />
    public async ValueTask<Result<Order>> Handle(CreateDraftOrderCommand command, CancellationToken cancellationToken)
    {
        var actor = _actorProvider.GetCurrentActor();

        // Fetch customer and all products in parallel
        var customerTask = _customerRepository.FindByIdAsync(command.CustomerId, cancellationToken);
        var productTasks = command.LineItems
            .Select(li => _productRepository.FindByIdAsync(li.ProductId, cancellationToken))
            .ToArray();

        var allTasks = new List<Task>(productTasks.Length + 1) { customerTask };
        allTasks.AddRange(productTasks);
        await Task.WhenAll(allTasks);

        var customerResult = (await customerTask)
            .ToResult(Error.NotFound($"Customer {command.CustomerId} not found.", "customerId"));
        if (customerResult.IsFailure)
            return customerResult.Error;

        var lineItemInputs = new List<OrderLineItemInput>(command.LineItems.Count);
        for (var i = 0; i < command.LineItems.Count; i++)
        {
            var productResult = (await productTasks[i])
                .ToResult(Error.NotFound($"Product {command.LineItems[i].ProductId} not found.", "productId"));
            if (productResult.IsFailure)
                return productResult.Error;

            lineItemInputs.Add(new OrderLineItemInput(
                command.LineItems[i].ProductId,
                productResult.Value.ProductName,
                command.LineItems[i].Quantity,
                productResult.Value.UnitPrice));
        }

        return await Order.TryCreate(command.CustomerId, actor.Id, lineItemInputs)
            .BindAsync(order => _orderRepository.SaveAsync(order, cancellationToken).MapAsync(_ => order));
    }
}
