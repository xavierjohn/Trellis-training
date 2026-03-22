namespace OrderManagement.Application.Commands;

using Mediator;
using Trellis.Authorization;
using Trellis.Mediator;
using Trellis.Primitives;

public sealed record LineItemRequest(ProductId ProductId, int Quantity);

public sealed record CreateDraftOrderCommand(
    CustomerId CustomerId,
    List<LineItemRequest> LineItems)
    : ICommand<Result<Order>>, IAuthorize, IValidate
{
    public IReadOnlyList<string> RequiredPermissions => [Permissions.OrdersCreate];

    public IResult Validate()
    {
        if (LineItems.Count == 0)
            return Result.Failure(Error.Validation("At least one line item is required", "lineItems"));

        var duplicates = LineItems
            .GroupBy(li => li.ProductId)
            .Where(g => g.Count() > 1)
            .ToList();
        if (duplicates.Count > 0)
            return Result.Failure(Error.Validation("Duplicate products in line items are not allowed", "lineItems"));

        Error? err = null;
        foreach (var item in LineItems)
        {
            if (item.Quantity < 1 || item.Quantity > 999)
                err = err.Combine(Error.Validation($"Quantity for product {item.ProductId} must be between 1 and 999", "lineItems"));
        }

        return err is not null ? Result.Failure(err) : Result.Success();
    }
}
