namespace OrderManagement.Application.Orders;

using FluentValidation;
using Mediator;
using OrderManagement.Application.Customers;
using OrderManagement.Application.Products;
using OrderManagement.Domain;
using Trellis.Authorization;

/// <summary>A line on a <see cref="CreateDraftOrderCommand"/>. Pairs a product with a quantity.</summary>
public sealed record DraftLineItem(ProductId ProductId, LineItemQuantity Quantity);

/// <summary>
/// Creates a new draft order for a customer with one or more line items. Unit prices
/// are captured from the product catalog at draft creation time, so subsequent price
/// changes do not retroactively affect the order. Stock is NOT reserved at this stage —
/// that happens on submission.
/// </summary>
public sealed record CreateDraftOrderCommand(CustomerId CustomerId, IReadOnlyList<DraftLineItem> LineItems)
    : ICommand<Result<Order>>, IAuthorize
{
    /// <inheritdoc />
    public IReadOnlyList<string> RequiredPermissions { get; } = [Permissions.OrdersCreate];
}

public sealed class CreateDraftOrderCommandValidator : AbstractValidator<CreateDraftOrderCommand>
{
    public CreateDraftOrderCommandValidator()
    {
        RuleFor(c => c.CustomerId).NotNull();
        RuleFor(c => c.LineItems)
            .NotNull()
            .NotEmpty().WithMessage("Order must have at least one line item.")
            .Must(HaveNoDuplicateProductIds)
            .WithMessage("Order must not contain duplicate productIds; combine quantities instead.");
        RuleForEach(c => c.LineItems).ChildRules(li =>
        {
            li.RuleFor(x => x.ProductId).NotNull();
            li.RuleFor(x => x.Quantity).NotNull();
        });
    }

    private static bool HaveNoDuplicateProductIds(IReadOnlyList<DraftLineItem> lineItems) =>
        lineItems.Select(li => li.ProductId).Distinct().Count() == lineItems.Count;
}

public sealed class CreateDraftOrderCommandHandler : ICommandHandler<CreateDraftOrderCommand, Result<Order>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IActorProvider _actorProvider;
    private readonly TimeProvider _timeProvider;

    public CreateDraftOrderCommandHandler(
        IOrderRepository orderRepository,
        ICustomerRepository customerRepository,
        IProductRepository productRepository,
        IActorProvider actorProvider,
        TimeProvider timeProvider)
    {
        _orderRepository = orderRepository;
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _actorProvider = actorProvider;
        _timeProvider = timeProvider;
    }

    public async ValueTask<Result<Order>> Handle(CreateDraftOrderCommand command, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.FindByIdAsync(command.CustomerId, cancellationToken);
        if (!customer.TryGetValue(out _))
            return Result.Fail<Order>(new Error.NotFound(ResourceRef.For<Customer>(command.CustomerId))
            { Detail = $"Customer {command.CustomerId} not found." });

        var requestedIds = command.LineItems.Select(li => li.ProductId).ToList();
        var products = await _productRepository.FindManyByIdAsync(requestedIds, cancellationToken);
        var productsById = products.ToDictionary(p => p.Id);

        var missing = requestedIds.FirstOrDefault(id => !productsById.ContainsKey(id));
        if (missing is not null)
            return Result.Fail<Order>(new Error.NotFound(ResourceRef.For<Product>(missing))
            { Detail = $"Product {missing} not found." });

        var actor = (await _actorProvider.GetCurrentActorAsync(cancellationToken))
            .GetValueOrThrow("Actor must be present; IAuthorize pipeline guarantees this.");

        var actorIdResult = OrderManagement.Domain.ActorId.TryCreate(actor.Id);
        if (!actorIdResult.TryGetValue(out var actorId))
            return Result.Fail<Order>(actorIdResult.Error!);

        var order = new Order(command.CustomerId, actorId, _timeProvider);
        foreach (var draftLine in command.LineItems)
        {
            var product = productsById[draftLine.ProductId];
            var addResult = order.AddLineItem(product.Id, product.ProductName, draftLine.Quantity, product.UnitPrice);
            if (addResult.IsFailure)
                return Result.Fail<Order>(addResult.Error!);
        }

        _orderRepository.Add(order);
        return Result.Ok(order);
    }
}
