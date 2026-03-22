namespace OrderManagement.Domain;

using Trellis.Primitives;

public class Product : Aggregate<ProductId>
{
    public ProductName ProductName { get; private set; } = default!;
    public Sku Sku { get; private set; } = default!;
    public Money UnitPrice { get; private set; } = default!;
    public int StockQuantity { get; private set; }

    public static Result<Product> TryCreate(ProductName productName, Sku sku, Money unitPrice)
    {
        var product = new Product(
            ProductId.NewUniqueV4(),
            productName,
            sku,
            unitPrice,
            0);
        return product;
    }

    public Result<Product> AddStock(int quantity)
    {
        if (quantity <= 0)
            return Error.Validation("Quantity must be positive", "quantity");

        StockQuantity += quantity;
        return this;
    }

    public Result<Product> ReserveStock(int quantity)
    {
        if (quantity <= 0)
            return Error.Validation("Quantity must be positive", "quantity");

        if (StockQuantity < quantity)
            return Error.Domain($"Insufficient stock for product '{ProductName}'. Available: {StockQuantity}, Requested: {quantity}");

        StockQuantity -= quantity;
        return this;
    }

    public Result<Product> ReleaseStock(int quantity)
    {
        if (quantity <= 0)
            return Error.Validation("Quantity must be positive", "quantity");

        StockQuantity += quantity;
        return this;
    }

    private Product(
        ProductId id,
        ProductName productName,
        Sku sku,
        Money unitPrice,
        int stockQuantity) : base(id)
    {
        ProductName = productName;
        Sku = sku;
        UnitPrice = unitPrice;
        StockQuantity = stockQuantity;
    }

    // EF Core constructor
    private Product() : base(default!)
    {
    }
}
