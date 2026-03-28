namespace OrderManagement.Domain;

using Trellis.Primitives;

/// <summary>
/// A single line item in an order, capturing product details at the time the order was created.
/// Owned by the Order aggregate.
/// </summary>
public class LineItem : Entity<LineItemId>
{
    /// <summary>Reference to the product.</summary>
    public ProductId ProductId { get; private set; } = null!;

    /// <summary>Snapshot of the product name at the time the line item was added.</summary>
    public ProductName ProductName { get; private set; } = null!;

    /// <summary>Quantity ordered.</summary>
    public LineItemQuantity Quantity { get; private set; } = null!;

    /// <summary>Snapshot of the unit price at the time the line item was added.</summary>
    public Money UnitPrice { get; private set; } = null!;

    /// <summary>EF Core constructor.</summary>
    private LineItem() : base(default!) { }

    internal LineItem(LineItemId id, ProductId productId, ProductName productName, LineItemQuantity quantity, Money unitPrice)
        : base(id)
    {
        ProductId = productId;
        ProductName = productName;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }
}
