namespace OrderManagement.Domain;

using Trellis.Primitives;

/// <summary>
/// Product aggregate with inventory tracking.
/// </summary>
public class Product : Aggregate<ProductId>
{
    public ProductName ProductName { get; private set; } = null!;
    public Sku Sku { get; private set; } = null!;
    public Money UnitPrice { get; private set; } = null!;
    public StockQuantity StockQuantity { get; private set; } = null!;

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
        Result.Ensure(unitPrice.Amount > 0,
                Error.Validation("Unit price must be greater than zero.", "unitPrice"))
            .Map(_ => new Product(productName, sku, unitPrice));

    public Result<Product> AddStock(StockQuantity quantity) =>
        Result.Ensure(quantity.Value > 0,
                Error.Validation("Quantity must be positive.", "quantity"))
            .Bind(_ => StockQuantity.TryCreate(StockQuantity.Value + quantity.Value))
            .Map(newQty =>
            {
                StockQuantity = newQty;
                return this;
            });

    public Result<Product> ReserveStock(StockQuantity quantity) =>
        Result.Ensure(StockQuantity.Value >= quantity.Value,
                Error.Validation($"Insufficient stock. Available: {StockQuantity.Value}, requested: {quantity.Value}.", "quantity"))
            .Bind(_ => StockQuantity.TryCreate(StockQuantity.Value - quantity.Value))
            .Map(newQty =>
            {
                StockQuantity = newQty;
                return this;
            });

    public Result<Product> ReleaseStock(StockQuantity quantity) =>
        StockQuantity.TryCreate(StockQuantity.Value + quantity.Value)
            .Map(newQty =>
            {
                StockQuantity = newQty;
                return this;
            });
}
