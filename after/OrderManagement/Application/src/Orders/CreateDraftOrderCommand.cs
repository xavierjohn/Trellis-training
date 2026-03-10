namespace OrderManagement.Application.Orders;

using Mediator;
using OrderManagement.Domain.Aggregates;
using OrderManagement.Domain.ValueObjects;
using Trellis.Authorization;
using Trellis.Mediator;

public sealed record LineItemInput(ProductId ProductId, LineItemQuantity Quantity);

public sealed record CreateDraftOrderCommand(
    CustomerId CustomerId,
    List<LineItemInput> LineItems) : ICommand<Result<Order>>, IAuthorize, IValidate
{
    public IReadOnlyList<string> RequiredPermissions => [Domain.Permissions.OrdersCreate];

    public IResult Validate()
    {
        if (LineItems.Count == 0)
        {
            return Result.Failure(Error.Validation("At least one line item is required.", "lineItems"));
        }

        var duplicateProducts = LineItems
            .GroupBy(li => li.ProductId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateProducts.Count > 0)
        {
            return Result.Failure(Error.Validation("Duplicate products are not allowed. Combine quantities instead.", "lineItems"));
        }

        return Result.Success();
    }
}
