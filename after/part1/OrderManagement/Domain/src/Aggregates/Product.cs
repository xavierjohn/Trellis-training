namespace OrderManagement.Domain;

using Trellis.Primitives;

/// <summary>
/// A product available for purchase with an inventory tracked by stock quantity.
/// </summary>
public class Product : Aggregate<ProductId>
{
    /// <summary>Display name of the product.</summary>
    public ProductName ProductName { get; private set; } = null!;

    /// <summary>Unique stock keeping unit code.</summary>
    public Sku Sku { get; private set; } = null!;

    /// <summary>Unit price in USD.</summary>
    public Money UnitPrice { get; private set; } = null!;

    /// <summary>Current available stock quantity.</summary>
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

    /// <summary>Creates a new product with zero stock.</summary>
    public static Result<Product> TryCreate(ProductName productName, Sku sku, Money unitPrice) =>
        Result.Ensure(unitPrice.IsGreaterThan(Money.Create(0m, "USD")),
                Error.Validation("Unit price must be greater than zero.", "unitPrice"))
            .Map(_ => new Product(productName, sku, unitPrice));

    /// <summary>Increases stock quantity. Quantity must be positive.</summary>
    public Result<Product> AddStock(StockQuantity quantity) =>
        Result.Ensure(quantity.Value > 0,
                Error.Validation("Quantity to add must be positive.", "quantity"))
            .Map(_ =>
            {
                StockQuantity = StockQuantity.Create(StockQuantity.Value + quantity.Value);
                return this;
            });

    /// <summary>Decreases stock quantity for reservation. Fails if insufficient stock.</summary>
    public Result<Product> ReserveStock(LineItemQuantity quantity) =>
        Result.Ensure(StockQuantity.Value >= quantity.Value,
                Error.Validation($"Insufficient stock. Available: {StockQuantity.Value}, requested: {quantity.Value}.", "quantity"))
            .Map(_ =>
            {
                StockQuantity = StockQuantity.Create(StockQuantity.Value - quantity.Value);
                return this;
            });

    /// <summary>Restores previously reserved stock (e.g., on order cancellation).</summary>
    public Product ReleaseStock(LineItemQuantity quantity)
    {
        StockQuantity = StockQuantity.Create(StockQuantity.Value + quantity.Value);
        return this;
    }
}
