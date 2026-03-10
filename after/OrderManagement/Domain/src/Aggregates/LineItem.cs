namespace OrderManagement.Domain.Aggregates;

using OrderManagement.Domain.ValueObjects;
using Trellis.Primitives;

public class LineItem : Entity<LineItemId>
{
    public ProductId ProductId { get; private set; } = null!;
    public ProductName ProductName { get; private set; } = null!;
    public LineItemQuantity Quantity { get; private set; } = null!;
    public Money UnitPrice { get; private set; } = null!;

    private LineItem() : base(default!) { }

    public static Result<LineItem> TryCreate(
        ProductId productId,
        ProductName productName,
        LineItemQuantity quantity,
        Money unitPrice)
    {
        var lineItem = new LineItem
        {
            Id = LineItemId.NewUniqueV7(),
            ProductId = productId,
            ProductName = productName,
            Quantity = quantity,
            UnitPrice = unitPrice
        };

        return lineItem;
    }
}
