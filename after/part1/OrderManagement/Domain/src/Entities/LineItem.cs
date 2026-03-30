namespace OrderManagement.Domain;

using Trellis.Primitives;

/// <summary>
/// A line item within an order.
/// </summary>
public class LineItem : Entity<LineItemId>
{
    public ProductId ProductId { get; private set; } = null!;
    public ProductName ProductName { get; private set; } = null!;
    public LineItemQuantity Quantity { get; private set; } = null!;
    public Money UnitPrice { get; private set; } = null!;

    private LineItem() : base(default!) { }

    public LineItem(ProductId productId, ProductName productName, LineItemQuantity quantity, Money unitPrice)
        : base(LineItemId.NewUniqueV7())
    {
        ProductId = productId;
        ProductName = productName;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }
}
