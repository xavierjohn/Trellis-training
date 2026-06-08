namespace OrderManagement.Domain;

/// <summary>
/// A line in an <see cref="Order"/>: a (Product, quantity, snapshot-of-unit-price) tuple.
/// Unit price is captured at the time the line item is added and does NOT change if the
/// product price changes later.
/// </summary>
public partial class LineItem : Entity<LineItemId>
{
    public ProductId ProductId { get; private set; } = null!;
    public ProductName ProductName { get; private set; } = null!;
    public LineItemQuantity Quantity { get; private set; } = null!;
    public UnitPrice UnitPrice { get; private set; } = null!;

    /// <summary>Sub-total: Quantity * UnitPrice.</summary>
    public decimal LineTotal => Quantity.Value * UnitPrice.Value;

    /// <summary>EF Core constructor.</summary>
    private LineItem() : base(default!) { }

    public LineItem(ProductId productId, ProductName productName, LineItemQuantity quantity, UnitPrice unitPrice)
        : base(LineItemId.NewUniqueV7())
    {
        ProductId = productId;
        ProductName = productName;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }
}
