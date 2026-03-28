namespace OrderManagement.Domain;

using Trellis.Primitives;

/// <summary>
/// Product aggregate with name, SKU, unit price, and stock quantity.
/// </summary>
public class Product : Aggregate<ProductId>
{
    public ProductName ProductName { get; private set; } = null!;
    public Sku Sku { get; private set; } = null!;
    public Money UnitPrice { get; private set; } = null!;
    public StockQuantity StockQuantity { get; private set; } = null!;

    /// <summary>EF Core constructor.</summary>
    private Product() : base(default!) { }

    private Product(ProductName productName, Sku sku, Money unitPrice)
        : base(ProductId.NewUniqueV7())
    {
        ProductName = productName;
        Sku = sku;
        UnitPrice = unitPrice;
        StockQuantity = StockQuantity.Create(0);
    }

    public static Result<Product> TryCreate(ProductName productName, Sku sku, Money unitPrice) =>
        new Product(productName, sku, unitPrice);

    /// <summary>
    /// Increases stock quantity. Quantity must be positive.
    /// </summary>
    public Result<Product> AddStock(StockQuantity quantity) =>
        Result.Ensure(quantity.Value > 0, Error.Validation("Quantity must be positive.", "quantity"))
            .Map(_ =>
            {
                StockQuantity = StockQuantity.Create(StockQuantity.Value + quantity.Value);
                return this;
            });

    /// <summary>
    /// Decreases stock quantity. Fails if insufficient stock.
    /// </summary>
    public Result<Product> ReserveStock(LineItemQuantity quantity) =>
        Result.Ensure(StockQuantity.Value >= quantity.Value,
                Error.Validation($"Insufficient stock. Available: {StockQuantity.Value}, Requested: {quantity.Value}.", "stockQuantity"))
            .Map(_ =>
            {
                StockQuantity = StockQuantity.Create(StockQuantity.Value - quantity.Value);
                return this;
            });

    /// <summary>
    /// Releases previously reserved stock.
    /// </summary>
    public Result<Product> ReleaseStock(LineItemQuantity quantity)
    {
        StockQuantity = StockQuantity.Create(StockQuantity.Value + quantity.Value);
        return this;
    }
}
