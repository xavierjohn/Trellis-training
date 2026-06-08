namespace OrderManagement.Domain;

/// <summary>
/// An item available for purchase. Identified by <see cref="ProductId"/> and
/// addressable by its unique <see cref="Sku"/>.
/// </summary>
public partial class Product : Aggregate<ProductId>
{
    public ProductName ProductName { get; private set; } = null!;
    public Sku Sku { get; private set; } = null!;
    public UnitPrice UnitPrice { get; private set; } = null!;
    public StockQuantity StockQuantity { get; private set; } = null!;

    /// <summary>EF Core constructor.</summary>
    private Product() : base(default!) { }

    public Product(ProductName productName, Sku sku, UnitPrice unitPrice)
        : base(ProductId.NewUniqueV7())
    {
        ProductName = productName;
        Sku = sku;
        UnitPrice = unitPrice;

        if (!StockQuantity.TryCreate(0).TryGetValue(out var initialStock))
            throw new InvalidOperationException("StockQuantity.TryCreate(0) must succeed — 0 is a valid stock quantity.");
        StockQuantity = initialStock;
    }

    /// <summary>
    /// Increases stock by the given positive amount.
    /// </summary>
    public Result<StockQuantity> AddStock(int quantity)
    {
        if (quantity <= 0)
            return Result.Fail<StockQuantity>(
                Error.InvalidInput.ForField("quantity", "product.add-stock.non-positive", "Quantity to add must be positive."));

        return StockQuantity.TryCreate(StockQuantity.Value + quantity)
            .Tap(updated => StockQuantity = updated);
    }

    /// <summary>
    /// Decreases stock by the given positive amount. Fails if insufficient stock.
    /// </summary>
    public Result<StockQuantity> ReserveStock(int quantity)
    {
        if (quantity <= 0)
            return Result.Fail<StockQuantity>(
                Error.InvalidInput.ForField("quantity", "product.reserve-stock.non-positive", "Quantity to reserve must be positive."));

        if (StockQuantity.Value < quantity)
            return Result.Fail<StockQuantity>(
                Error.InvalidInput.ForRule(
                    "product.insufficient-stock",
                    $"Product '{ProductName.Value}' has insufficient stock: requested {quantity}, available {StockQuantity.Value}."));

        return StockQuantity.TryCreate(StockQuantity.Value - quantity)
            .Tap(updated => StockQuantity = updated);
    }

    /// <summary>
    /// Releases reserved stock — increases stock back by the given positive amount.
    /// Used by the Cancel-Order path when a Submitted/Approved order is cancelled
    /// to restore the reserved-but-not-yet-shipped quantities.
    /// </summary>
    public Result<StockQuantity> ReleaseStock(int quantity) => AddStock(quantity);
}
