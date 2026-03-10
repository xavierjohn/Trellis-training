namespace OrderManagement.Domain.Aggregates;

using OrderManagement.Domain.ValueObjects;
using Trellis.Primitives;

public class Product : Aggregate<ProductId>
{
    public ProductName ProductName { get; private set; } = null!;
    public Sku Sku { get; private set; } = null!;
    public Money UnitPrice { get; private set; } = null!;
    public StockQuantity StockQuantity { get; private set; } = null!;

    private Product() : base(default!) { }

    public static Result<Product> TryCreate(
        ProductName productName,
        Sku sku,
        Money unitPrice)
    {
        if (unitPrice.Amount <= 0)
        {
            return Error.Validation("Unit price must be greater than zero.", "unitPrice");
        }

        var product = new Product
        {
            Id = ProductId.NewUniqueV7(),
            ProductName = productName,
            Sku = sku,
            UnitPrice = unitPrice,
            StockQuantity = StockQuantity.Zero
        };

        return product;
    }

    public Result<Product> AddStock(int quantity)
    {
        return StockQuantity.Add(quantity)
            .Tap(sq => StockQuantity = sq)
            .Map(_ => this);
    }

    public Result<Product> ReserveStock(int quantity)
    {
        return StockQuantity.Reserve(quantity)
            .Tap(sq => StockQuantity = sq)
            .Map(_ => this);
    }

    public Result<Product> ReleaseStock(int quantity)
    {
        return StockQuantity.Release(quantity)
            .Tap(sq => StockQuantity = sq)
            .Map(_ => this);
    }
}
