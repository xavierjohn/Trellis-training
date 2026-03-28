namespace OrderManagement.Domain;

using Trellis.Primitives;

/// <summary>
/// A single entry in an order specifying a product, quantity, and unit price.
/// </summary>
public class LineItem : Entity<LineItemId>
{
    public ProductId ProductId { get; private set; } = null!;
    public ProductName ProductName { get; private set; } = null!;
    public LineItemQuantity Quantity { get; private set; } = null!;
    public Money UnitPrice { get; private set; } = null!;

    /// <summary>EF Core constructor.</summary>
    private LineItem() : base(default!) { }

    internal LineItem(ProductId productId, ProductName productName, LineItemQuantity quantity, Money unitPrice)
        : base(LineItemId.NewUniqueV7())
    {
        ProductId = productId;
        ProductName = productName;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    /// <summary>
    /// Creates a new line item (public factory for Application layer).
    /// </summary>
    public static LineItem Create(ProductId productId, ProductName productName, LineItemQuantity quantity, Money unitPrice) =>
        new(productId, productName, quantity, unitPrice);
}
