namespace TrellisArm;

/// <summary>
/// A product with a finite stock that orders draw down. <see cref="Aggregate{TId}"/> gives it an
/// ETag the Trellis EF integration configures as a concurrency token, so a lost update on
/// <see cref="Stock"/> is detected at commit time rather than silently oversold (R1).
/// </summary>
public class Product : Aggregate<ProductId>
{
    public string Name { get; private set; } = "";
    public decimal Price { get; private set; }
    public StockQuantity Stock { get; private set; } = null!;

    /// <summary>EF Core constructor.</summary>
    private Product() : base(default!) { }

    private Product(string name, decimal price, StockQuantity stock) : base(ProductId.NewUniqueV7())
    {
        Name = name;
        Price = price;
        Stock = stock;
    }

    /// <summary>Creates a product, validating name, price, and the stock invariant up front.</summary>
    public static Result<Product> Create(string name, int stock, decimal price)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Fail<Product>(
                Error.InvalidInput.ForField("name", "product.name.required", "Product name is required."));

        if (price < 0)
            return Result.Fail<Product>(
                Error.InvalidInput.ForField("price", "product.price.negative", "Price cannot be negative."));

        return StockQuantity.TryCreate(stock).Map(s => new Product(name, price, s));
    }

    /// <summary>
    /// Draws <paramref name="quantity"/> units down from stock. Fails (rather than throwing or
    /// going negative) when stock is insufficient — the failure is a value, not an exception (R3),
    /// and the <see cref="StockQuantity"/> invariant means it can never persist a negative (R6).
    /// </summary>
    public Result<StockQuantity> ReserveStock(int quantity)
    {
        if (Stock.Value < quantity)
            return Result.Fail<StockQuantity>(
                Error.InvalidInput.ForRule(
                    "product.insufficient-stock",
                    $"Product '{Name}' has insufficient stock: requested {quantity}, available {Stock.Value}."));

        return StockQuantity.TryCreate(Stock.Value - quantity).Tap(updated => Stock = updated);
    }
}

/// <summary>A line on an order: a (product, quantity) pair. Owned by the <see cref="Order"/>.</summary>
public class LineItem : Entity<LineItemId>
{
    public ProductId ProductId { get; private set; } = null!;
    public Quantity Quantity { get; private set; } = null!;

    /// <summary>EF Core constructor.</summary>
    private LineItem() : base(default!) { }

    public LineItem(ProductId productId, Quantity quantity) : base(LineItemId.NewUniqueV7())
    {
        ProductId = productId;
        Quantity = quantity;
    }
}
