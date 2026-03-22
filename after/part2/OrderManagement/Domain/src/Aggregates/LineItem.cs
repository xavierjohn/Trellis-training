namespace OrderManagement.Domain;

using Trellis.Primitives;

public class LineItem : Entity<LineItemId>
{
    public ProductId ProductId { get; private set; } = default!;
    public string ProductName { get; private set; } = default!;
    public int Quantity { get; private set; }
    public Money UnitPrice { get; private set; } = default!;

    public Money Total
    {
        get
        {
            if (UnitPrice.Multiply(Quantity).TryGetValue(out var total))
                return total;
            return UnitPrice;
        }
    }

    internal static Result<LineItem> TryCreate(
        ProductId productId,
        string productName,
        int quantity,
        Money unitPrice)
    {
        if (string.IsNullOrWhiteSpace(productName))
            return Error.Validation("Product name is required", "productName");
        if (quantity < 1 || quantity > 999)
            return Error.Validation("Quantity must be between 1 and 999", "quantity");

        return new LineItem(LineItemId.NewUniqueV4(), productId, productName, quantity, unitPrice);
    }

    private LineItem(
        LineItemId id,
        ProductId productId,
        string productName,
        int quantity,
        Money unitPrice) : base(id)
    {
        ProductId = productId;
        ProductName = productName;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    // EF Core constructor
    private LineItem() : base(default!)
    {
    }
}
